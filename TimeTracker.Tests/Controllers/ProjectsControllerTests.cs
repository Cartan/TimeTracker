using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TimeTracker.Controllers;
using TimeTracker.Data;
using TimeTracker.Models;
using TimeTracker.ViewModels;
using Xunit;

namespace TimeTracker.Tests.Controllers;

public class ProjectsControllerTests
{
    [Fact]
    public async Task Create_PersistsProject()
    {
        await using var db = CreateDbContext();
        var customer = new Customer { Name = "Contoso" };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var controller = new ProjectsController(db);

        var result = await controller.Create(new ProjectCreateViewModel
        {
            Name = "Website Redesign",
            CustomerId = customer.Id
        });

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);

        var project = await db.Projects.SingleAsync();
        Assert.Equal("Website Redesign", project.Name);
        Assert.Equal(customer.Id, project.CustomerId);
    }

    [Fact]
    public async Task Create_DoesNotPersistProjectForUnknownCustomer()
    {
        await using var db = CreateDbContext();
        db.Customers.Add(new Customer { Name = "Contoso" });
        await db.SaveChangesAsync();

        var controller = new ProjectsController(db);

        var result = await controller.Create(new ProjectCreateViewModel
        {
            Name = "Website Redesign",
            CustomerId = int.MaxValue
        });

        Assert.IsType<ViewResult>(result);
        Assert.Empty(await db.Projects.ToListAsync());
        Assert.True(controller.ModelState.ContainsKey(nameof(ProjectCreateViewModel.CustomerId)));
    }

    private static TimeTrackerDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<TimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new TimeTrackerDbContext(options);
    }
}
