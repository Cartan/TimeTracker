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
    public async Task Create_AssignsCustomerToCurrentUser()
    {
        await using var db = CreateDbContext();
        var alice = new User { Login = "alice@example.com" };
        db.Users.Add(alice);
        await db.SaveChangesAsync();
        db.CurrentUserId = alice.Id;

        await new CustomersController(db).Create(new Customer { Name = "Fabrikam" });

        Assert.Equal(alice.Id, (await db.Customers.SingleAsync()).UserId);
    }

    [Fact]
    public async Task Users_OnlySeeAndDeleteTheirOwnCustomers()
    {
        await using var db = CreateDbContext();
        var alice = new User { Login = "alice@example.com" };
        var bob = new User { Login = "bob@example.com" };
        db.Users.AddRange(alice, bob);
        db.Customers.AddRange(
            new Customer { Name = "Alice's customer", User = alice },
            new Customer { Name = "Bob's customer", User = bob });
        await db.SaveChangesAsync();
        var bobsCustomerId = (await db.Customers.IgnoreQueryFilters().SingleAsync(c => c.UserId == bob.Id)).Id;

        db.CurrentUserId = alice.Id;
        var controller = new CustomersController(db);

        var index = Assert.IsType<ViewResult>(await controller.Index());
        var visible = Assert.IsAssignableFrom<IEnumerable<Customer>>(index.Model);
        Assert.Equal("Alice's customer", Assert.Single(visible).Name);

        Assert.IsType<NotFoundResult>(await controller.Delete(bobsCustomerId));
        Assert.Equal(2, await db.Customers.IgnoreQueryFilters().CountAsync());
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
