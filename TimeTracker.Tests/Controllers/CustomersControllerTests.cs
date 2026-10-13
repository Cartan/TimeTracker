using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TimeTracker.Controllers;
using TimeTracker.Data;
using TimeTracker.Models;
using Xunit;

namespace TimeTracker.Tests.Controllers;

public class CustomersControllerTests
{
    [Fact]
    public async Task Delete_RemovesCustomerProjectsAndTimeEntries()
    {
        await using var db = CreateDbContext();
        var customer = new Customer { Name = "Contoso" };
        var project = new Project { Name = "Website Redesign", Customer = customer };
        project.TimeEntries.Add(new TimeEntry { StartTime = DateTime.UtcNow });
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var controller = new CustomersController(db);

        var result = await controller.Delete(customer.Id);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Empty(await db.Customers.ToListAsync());
        Assert.Empty(await db.Projects.ToListAsync());
        Assert.Empty(await db.TimeEntries.ToListAsync());
    }

    [Fact]
    public async Task Delete_ReturnsNotFoundForUnknownCustomer()
    {
        await using var db = CreateDbContext();
        var controller = new CustomersController(db);

        var result = await controller.Delete(int.MaxValue);

        Assert.IsType<NotFoundResult>(result);
    }

    private static TimeTrackerDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<TimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new TimeTrackerDbContext(options);
    }
}
