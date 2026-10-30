using System.Text;
using System.Security.Claims;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using TimeTracker.Data;
using TimeTracker.Models;
using TimeTracker.Services;
using Xunit;

namespace TimeTracker.Tests.Services;

public class TenantMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_CreatesConfiguredInitialUserWhenMissing()
    {
        await using var db = CreateDbContext();
        const string login = "user@example.com";
        var context = new DefaultHttpContext();
        context.Request.Headers["X-MS-CLIENT-PRINCIPAL-NAME"] = login;
        var nextCalled = false;
        var middleware = new TenantMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Tenancy:InitialUsers:0"] = login
            })
            .Build();

        await middleware.InvokeAsync(context, db, configuration, new TestWebHostEnvironment());

        Assert.True(nextCalled);
        Assert.Equal(login, (await db.Users.SingleAsync()).Login);
    }

    [Fact]
    public async Task InvokeAsync_UsesLoginFromAppServiceIdToken()
    {
        await using var db = CreateDbContext();
        var user = new User { Login = "user@example.com" };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var context = new DefaultHttpContext();
        context.Request.Headers["X-MS-TOKEN-AAD-ID-TOKEN"] = CreateJwt(
            """{"preferred_username":"user@example.com"}""");
        var nextCalled = false;
        var middleware = new TenantMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(
            context,
            db,
            new ConfigurationBuilder().Build(),
            new TestWebHostEnvironment());

        Assert.True(nextCalled);
        Assert.Equal(user.Id, db.CurrentUserId);
    }

    [Fact]
    public async Task InvokeAsync_UsesLoginFromValidatedBearerToken()
    {
        await using var db = CreateDbContext();
        var user = new User { Login = "user@example.com" };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(
                new ClaimsIdentity([new Claim("preferred_username", user.Login)], "Bearer"))
        };
        var nextCalled = false;
        var middleware = new TenantMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(
            context,
            db,
            new ConfigurationBuilder().Build(),
            new TestWebHostEnvironment());

        Assert.True(nextCalled);
        Assert.Equal(user.Id, db.CurrentUserId);
    }

    [Fact]
    public async Task InvokeAsync_UsesLoginFromClientPrincipalWhenNameHeaderIsMissing()
    {
        await using var db = CreateDbContext();
        var user = new User { Login = "user@example.com" };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var context = new DefaultHttpContext();
        context.Request.Headers["X-MS-CLIENT-PRINCIPAL"] = Convert.ToBase64String(
                Encoding.UTF8.GetBytes(
                    """{"claims":[{"typ":"http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name","val":"user@example.com"}]}"""))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
        var nextCalled = false;
        var middleware = new TenantMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(
            context,
            db,
            new ConfigurationBuilder().Build(),
            new TestWebHostEnvironment());

        Assert.True(nextCalled);
        Assert.Equal(user.Id, db.CurrentUserId);
    }

    private static TimeTrackerDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<TimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new TimeTrackerDbContext(options);
    }

    private static string CreateJwt(string payload) =>
        $"header.{Convert.ToBase64String(Encoding.UTF8.GetBytes(payload)).TrimEnd('=').Replace('+', '-').Replace('/', '_')}.signature";

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = string.Empty;

        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();

        public string WebRootPath { get; set; } = string.Empty;

        public string EnvironmentName { get; set; } = Environments.Production;

        public string ContentRootPath { get; set; } = string.Empty;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
