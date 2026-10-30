using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TimeTracker.Controllers;
using TimeTracker.Data;
using TimeTracker.Dtos;
using TimeTracker.Models;
using Xunit;

namespace TimeTracker.Tests.Controllers;

public class TimeTrackerApiControllerTests
{
    [Fact]
    public async Task CreateCustomer_AssignsCustomerToCurrentUser()
    {
        await using var db = CreateDbContext();
        var user = new User { Login = "user@example.com" };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        db.CurrentUserId = user.Id;

        var result = await new TimeTrackerApiController(db)
            .CreateCustomer(new CreateCustomerRequest(" Fabrikam "), CancellationToken.None);

        var response = Assert.IsType<OkObjectResult>(result.Result);
        var customer = Assert.IsType<CustomerDto>(response.Value);
        Assert.Equal("Fabrikam", customer.Name);
        Assert.Equal(user.Id, (await db.Customers.SingleAsync()).UserId);
    }

    [Fact]
    public async Task CreateProject_CreatesProjectForCurrentUsersCustomer()
    {
        await using var db = CreateDbContext();
        var user = new User { Login = "user@example.com" };
        var customer = new Customer { Name = "Contoso", User = user };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        db.CurrentUserId = user.Id;

        var result = await new TimeTrackerApiController(db)
            .CreateProject(new CreateProjectRequest(" Website ", customer.Id), CancellationToken.None);

        var response = Assert.IsType<OkObjectResult>(result.Result);
        var project = Assert.IsType<ProjectDto>(response.Value);
        Assert.Equal("Website", project.Name);
        Assert.Equal(user.Id, (await db.Projects.SingleAsync()).UserId);
    }

    [Fact]
    public async Task CreateProject_DoesNotCreateProjectForAnotherUsersCustomer()
    {
        await using var db = CreateDbContext();
        var alice = new User { Login = "alice@example.com" };
        var bob = new User { Login = "bob@example.com" };
        var bobsCustomer = new Customer { Name = "Bob's customer", User = bob };
        db.Customers.AddRange(bobsCustomer);
        db.Users.Add(alice);
        await db.SaveChangesAsync();
        db.CurrentUserId = alice.Id;

        var result = await new TimeTrackerApiController(db)
            .CreateProject(new CreateProjectRequest("Private", bobsCustomer.Id), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
        Assert.Empty(await db.Projects.ToListAsync());
    }

    [Fact]
    public async Task StartTimeEntry_ClosesPreviousEntryAndStartsSelectedProject()
    {
        await using var db = CreateDbContext();
        var firstProject = new Project { Name = "First", Customer = new Customer { Name = "Contoso" } };
        var secondProject = new Project { Name = "Second", Customer = new Customer { Name = "Contoso" } };
        var previousEntry = new TimeEntry
        {
            Project = firstProject,
            StartTime = DateTime.UtcNow.AddHours(-2)
        };
        db.Projects.AddRange(firstProject, secondProject);
        db.TimeEntries.Add(previousEntry);
        await db.SaveChangesAsync();

        var result = await new TimeTrackerApiController(db)
            .StartTimeEntry(new StartTimeEntryRequest(secondProject.Id), CancellationToken.None);

        var response = Assert.IsType<OkObjectResult>(result.Result);
        var startedEntry = Assert.IsType<TimeEntryDto>(response.Value);
        Assert.Equal(secondProject.Id, startedEntry.ProjectId);
        Assert.NotNull(await db.TimeEntries.SingleAsync(t => t.Id == previousEntry.Id && t.EndTime != null));
        Assert.Equal(2, await db.TimeEntries.CountAsync());
    }

    [Fact]
    public async Task StartTimeEntry_ForAlreadyActiveProjectDoesNotCreateAnotherEntry()
    {
        await using var db = CreateDbContext();
        var project = new Project { Name = "Website", Customer = new Customer { Name = "Contoso" } };
        var activeEntry = new TimeEntry { Project = project, StartTime = DateTime.UtcNow.AddMinutes(-10) };
        db.TimeEntries.Add(activeEntry);
        await db.SaveChangesAsync();

        var result = await new TimeTrackerApiController(db)
            .StartTimeEntry(new StartTimeEntryRequest(project.Id), CancellationToken.None);

        var response = Assert.IsType<OkObjectResult>(result.Result);
        var returnedEntry = Assert.IsType<TimeEntryDto>(response.Value);
        Assert.Equal(activeEntry.Id, returnedEntry.Id);
        Assert.Single(await db.TimeEntries.ToListAsync());
        Assert.Null((await db.TimeEntries.SingleAsync()).EndTime);
    }

    [Fact]
    public async Task StartTimeEntry_ReturnsNotFoundForProjectOutsideCurrentTenant()
    {
        await using var db = CreateDbContext();
        var alice = new User { Login = "alice@example.com" };
        var bob = new User { Login = "bob@example.com" };
        db.Users.AddRange(alice, bob);
        await db.SaveChangesAsync();

        var bobsProject = new Project
        {
            Name = "Private project",
            User = bob,
            Customer = new Customer { Name = "Bob's customer", User = bob }
        };
        db.Projects.Add(bobsProject);
        await db.SaveChangesAsync();
        db.CurrentUserId = alice.Id;

        var result = await new TimeTrackerApiController(db)
            .StartTimeEntry(new StartTimeEntryRequest(bobsProject.Id), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
        Assert.Empty(await db.TimeEntries.ToListAsync());
    }

    [Fact]
    public async Task StopTimeEntry_StopsActiveEntry()
    {
        await using var db = CreateDbContext();
        var project = new Project { Name = "Website", Customer = new Customer { Name = "Contoso" } };
        var activeEntry = new TimeEntry { Project = project, StartTime = DateTime.UtcNow.AddMinutes(-10) };
        db.TimeEntries.Add(activeEntry);
        await db.SaveChangesAsync();

        var result = await new TimeTrackerApiController(db)
            .StopTimeEntry(activeEntry.Id, CancellationToken.None);

        var response = Assert.IsType<OkObjectResult>(result.Result);
        var stoppedEntry = Assert.IsType<TimeEntryDto>(response.Value);
        Assert.Equal(activeEntry.Id, stoppedEntry.Id);
        Assert.NotNull(stoppedEntry.EndTime);
        Assert.NotNull((await db.TimeEntries.SingleAsync()).EndTime);
    }

    [Fact]
    public async Task StopTimeEntry_ReturnsNotFoundWhenEntryIsNotActive()
    {
        await using var db = CreateDbContext();
        var result = await new TimeTrackerApiController(db)
            .StopTimeEntry(int.MaxValue, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    private static TimeTrackerDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<TimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new TimeTrackerDbContext(options);
    }
}
