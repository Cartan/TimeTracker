using Microsoft.EntityFrameworkCore;
using TimeTracker.Data;

namespace TimeTracker.Services;

// Maps the signed-in user (from the Easy Auth header) to a row in Users and scopes the request's DbContext to that user.
// Users that are not in the Users table get 403, so tenants are created by adding rows to that table.
public class TenantMiddleware
{
    private const string LoginHeader = "X-MS-CLIENT-PRINCIPAL-NAME";

    private readonly RequestDelegate _next;

    public TenantMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        TimeTrackerDbContext db,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        var login = context.Request.Headers[LoginHeader].FirstOrDefault()?.Trim();
        if (string.IsNullOrEmpty(login) && environment.IsDevelopment())
        {
            login = configuration["Tenancy:DevelopmentUser"]?.Trim();
        }

        var userId = string.IsNullOrEmpty(login)
            ? null
            : await db.Users
                .Where(u => u.Login == login)
                .Select(u => (int?)u.Id)
                .FirstOrDefaultAsync(context.RequestAborted);

        if (userId is null)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsync("Your account has not been set up for this application.");
            return;
        }

        db.CurrentUserId = userId;
        await _next(context);
    }
}
