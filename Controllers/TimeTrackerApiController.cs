using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TimeTracker.Data;
using TimeTracker.Dtos;

namespace TimeTracker.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TimeTrackerApiController : ControllerBase
{
    private readonly TimeTrackerDbContext _context;

    public TimeTrackerApiController(TimeTrackerDbContext context)
    {
        _context = context;
    }

    [HttpGet("customers")]
    public async Task<ActionResult<IEnumerable<CustomerDto>>> GetCustomers()
    {
        var customers = await _context.Customers
            .OrderBy(c => c.Name)
            .Select(c => new CustomerDto(c.Id, c.Name))
            .ToListAsync();

        return Ok(customers);
    }

    [HttpGet("projects")]
    public async Task<ActionResult<IEnumerable<ProjectDto>>> GetProjects()
    {
        var projects = await _context.Projects
            .Include(p => p.Customer)
            .OrderBy(p => p.Customer.Name)
            .ThenBy(p => p.Name)
            .Select(p => new ProjectDto(p.Id, p.CustomerId, p.Customer.Name, p.Name))
            .ToListAsync();

        return Ok(projects);
    }

    [HttpGet("timeentries")]
    public async Task<ActionResult<IEnumerable<TimeEntryDto>>> GetTimeEntries()
    {
        var entries = await _context.TimeEntries
            .Include(t => t.Project)
            .ThenInclude(p => p.Customer)
            .OrderByDescending(t => t.StartTime)
            .Select(t => new TimeEntryDto(
                t.Id,
                t.ProjectId,
                t.Project.Customer.Name + " - " + t.Project.Name,
                t.StartTime,
                t.EndTime,
                Math.Round((t.EndTime ?? DateTime.UtcNow) .Subtract(t.StartTime).TotalMinutes, 2)))
            .ToListAsync();

        return Ok(entries);
    }
}
