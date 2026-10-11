using Microsoft.EntityFrameworkCore;
using TimeTracker.Data;
using TimeTracker.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<TimeTrackerDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

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

    if (!db.Customers.Any())
    {
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

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseSession();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
