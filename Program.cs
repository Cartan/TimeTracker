using Microsoft.EntityFrameworkCore;
using TimeTracker.Data;
using TimeTracker.Models;
using TimeTracker.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<TimeTrackerDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sql => sql.EnableRetryOnFailure(
            maxRetryCount: 10,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorNumbersToAdd: null)));

builder.Services.AddControllersWithViews();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TimeTrackerDbContext>();
    db.Database.EnsureCreated();
    SchemaUpgrade.Apply(db);

    // Users listed here are created on startup, so there is always someone who can sign in.
    var initialLogins = builder.Configuration.GetSection("Tenancy:InitialUsers").Get<string[]>() ?? [];
    foreach (var login in initialLogins.Where(l => !string.IsNullOrWhiteSpace(l)).Select(l => l.Trim()).Distinct())
    {
        if (!db.Users.Any(u => u.Login == login))
        {
            db.Users.Add(new User { Login = login });
        }
    }

    db.SaveChanges();

    // Data created before tenants existed has no owner; it goes to the first initial user.
    var firstLogin = initialLogins.FirstOrDefault(l => !string.IsNullOrWhiteSpace(l))?.Trim();
    var owner = firstLogin is null ? null : db.Users.FirstOrDefault(u => u.Login == firstLogin);
    if (owner is not null)
    {
        db.Customers.Where(c => c.UserId == null).ExecuteUpdate(s => s.SetProperty(c => c.UserId, owner.Id));
        db.Projects.Where(p => p.UserId == null).ExecuteUpdate(s => s.SetProperty(p => p.UserId, owner.Id));
        db.TimeEntries.Where(t => t.UserId == null).ExecuteUpdate(s => s.SetProperty(t => t.UserId, owner.Id));

        if (!db.Customers.Any())
        {
            db.CurrentUserId = owner.Id;

            var customers = new[]
            {
                new Customer { Name = "Contoso" },
                new Customer { Name = "Northwind" }
            };

            db.Customers.AddRange(customers);
            db.SaveChanges();

            db.Projects.AddRange(
                new Project { CustomerId = customers[0].Id, Name = "Website Redesign" },
                new Project { CustomerId = customers[0].Id, Name = "CRM Integration" },
                new Project { CustomerId = customers[1].Id, Name = "Monthly Reporting" });

            db.SaveChanges();
        }
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseSession();
app.UseAuthorization();
app.UseMiddleware<TenantMiddleware>();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
