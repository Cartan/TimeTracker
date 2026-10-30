using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TimeTracker.Data;
using TimeTracker.Dtos;
using TimeTracker.Models;

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
        var now = DateTime.UtcNow;
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
                Math.Round((t.EndTime ?? now).Subtract(t.StartTime).TotalMinutes, 2)))
            .ToListAsync();

        return Ok(entries);
    }

    [HttpPost("timeentries/start")]
    public async Task<ActionResult<TimeEntryDto>> StartTimeEntry(
        [FromBody] StartTimeEntryRequest request,
        CancellationToken cancellationToken)
    {
        var project = await _context.Projects
            .Include(p => p.Customer)
            .FirstOrDefaultAsync(p => p.Id == request.ProjectId, cancellationToken);

        if (project is null)
        {
            return NotFound();
        }

        var now = DateTime.UtcNow;
        var activeEntry = await _context.TimeEntries
            .Include(t => t.Project)
            .ThenInclude(p => p.Customer)
            .Where(t => t.EndTime == null)
            .OrderByDescending(t => t.StartTime)
            .FirstOrDefaultAsync(cancellationToken);

        if (activeEntry?.ProjectId == project.Id)
        {
            return Ok(ToDto(activeEntry, now));
        }

        if (activeEntry is not null)
        {
            activeEntry.EndTime = now;
        }

        var newEntry = new TimeEntry
        {
            ProjectId = project.Id,
            Project = project,
            StartTime = now
        };

        _context.TimeEntries.Add(newEntry);
        await _context.SaveChangesAsync(cancellationToken);

        return Ok(ToDto(newEntry, now));
    }

    [HttpPost("timeentries/{id:int}/stop")]
    public async Task<ActionResult<TimeEntryDto>> StopTimeEntry(
        int id,
        CancellationToken cancellationToken)
    {
        var activeEntry = await _context.TimeEntries
            .Include(t => t.Project)
            .ThenInclude(p => p.Customer)
            .FirstOrDefaultAsync(
                t => t.Id == id && t.EndTime == null,
                cancellationToken);

        if (activeEntry is null)
        {
            return NotFound();
        }

        activeEntry.EndTime = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        return Ok(ToDto(activeEntry, activeEntry.EndTime.Value));
    }

    private static TimeEntryDto ToDto(TimeEntry entry, DateTime now)
    {
        var endTime = entry.EndTime ?? now;
        return new TimeEntryDto(
            entry.Id,
            entry.ProjectId,
            $"{entry.Project.Customer.Name} - {entry.Project.Name}",
            entry.StartTime,
            entry.EndTime,
            Math.Round((endTime - entry.StartTime).TotalMinutes, 2));
    }
}
