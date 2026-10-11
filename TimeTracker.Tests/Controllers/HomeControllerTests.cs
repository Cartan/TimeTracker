using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TimeTracker.Controllers;
using TimeTracker.Data;
using TimeTracker.Models;
using Xunit;

namespace TimeTracker.Tests.Controllers;

public class HomeControllerTests
{
    [Fact]
    public async Task StartTracking_AddsOpenTimeEntry_AndStoresLastProject()
    {
        var db = CreateDbContext();
        var customer = new Customer { Name = "Contoso" };
        var project = new Project { Name = "Portal Upgrade", Customer = customer };
        db.Customers.Add(customer);
        db.Projects.Add(project);
        await db.SaveChangesAsync();

        var controller = new HomeController(db)
        {
            ControllerContext = new ControllerContext { HttpContext = CreateHttpContext() }
        };

        var result = await controller.StartTracking(project.Id);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);

        var timeEntry = await db.TimeEntries.SingleAsync();
        Assert.Equal(project.Id, timeEntry.ProjectId);
        Assert.NotEqual(default, timeEntry.StartTime);
        Assert.Null(timeEntry.EndTime);
        Assert.Equal(project.Id, controller.HttpContext.Session.GetInt32("LastProjectId"));
    }

    [Fact]
    public async Task StopTracking_ClosesOpenEntry()
    {
        var db = CreateDbContext();
        var customer = new Customer { Name = "Northwind" };
        var project = new Project { Name = "Reports", Customer = customer };
        db.Customers.Add(customer);
        db.Projects.Add(project);
        await db.SaveChangesAsync();

        var active = new TimeEntry
        {
            ProjectId = project.Id,
            StartTime = DateTime.UtcNow.AddMinutes(-15),
            EndTime = null
        };
        db.TimeEntries.Add(active);
        await db.SaveChangesAsync();

        var controller = new HomeController(db)
        {
            ControllerContext = new ControllerContext { HttpContext = CreateHttpContext() }
        };

        var result = await controller.StopTracking(project.Id);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);

        var entry = await db.TimeEntries.SingleAsync();
        Assert.NotNull(entry.EndTime);
        Assert.True(entry.EndTime > entry.StartTime);
    }

    private static TimeTrackerDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<TimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new TimeTrackerDbContext(options);
    }

    private static HttpContext CreateHttpContext()
    {
        var context = new DefaultHttpContext();
        var session = new TestSession();
        context.Session = session;
        return context;
    }

    private sealed class TestSession : ISession
    {
        private readonly Dictionary<string, byte[]> _store = new();

        public IEnumerable<string> Keys => _store.Keys;
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public bool IsAvailable => true;

        public void Clear() => _store.Clear();

        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public void Remove(string key) => _store.Remove(key);

        public void Set(string key, byte[] value) => _store[key] = value;

        public bool TryGetValue(string key, out byte[] value) => _store.TryGetValue(key, out value!);
    }
}
