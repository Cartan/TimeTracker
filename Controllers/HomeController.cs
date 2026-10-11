using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TimeTracker.Data;
using TimeTracker.Models;
using TimeTracker.ViewModels;

namespace TimeTracker.Controllers;

public class HomeController : Controller
{
    private readonly TimeTrackerDbContext _context;

    public HomeController(TimeTrackerDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var projects = await _context.Projects
            .Include(p => p.Customer)
            .OrderBy(p => p.Customer.Name)
            .ThenBy(p => p.Name)
            .ToListAsync();

        var activeTimeEntry = await _context.TimeEntries
            .Include(t => t.Project)
            .ThenInclude(p => p.Customer)
            .Where(t => t.EndTime == null)
            .OrderByDescending(t => t.StartTime)
            .FirstOrDefaultAsync();

        var viewModel = new HomeIndexViewModel
        {
            Projects = projects,
            LastProjectId = HttpContext.Session.GetInt32("LastProjectId") ?? activeTimeEntry?.ProjectId,
            ActiveTimeEntry = activeTimeEntry
        };

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StartTracking(int projectId)
    {
        var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
        if (project is null)
        {
            return NotFound();
        }

        var activeEntry = await _context.TimeEntries
            .FirstOrDefaultAsync(t => t.EndTime == null);

        if (activeEntry is not null && activeEntry.ProjectId != projectId)
        {
            activeEntry.EndTime = DateTime.UtcNow;
        }

        if (activeEntry is null || activeEntry.ProjectId != projectId)
        {
            _context.TimeEntries.Add(new TimeEntry
            {
                ProjectId = projectId,
                StartTime = DateTime.UtcNow
            });
        }

        HttpContext.Session.SetInt32("LastProjectId", projectId);
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StopTracking(int projectId)
    {
        var activeEntry = await _context.TimeEntries
            .FirstOrDefaultAsync(t => t.EndTime == null && t.ProjectId == projectId);

        if (activeEntry is not null)
        {
            activeEntry.EndTime = DateTime.UtcNow;
            HttpContext.Session.SetInt32("LastProjectId", projectId);
            await _context.SaveChangesAsync();
        }
        else
        {
            HttpContext.Session.SetInt32("LastProjectId", projectId);
        }

        return RedirectToAction(nameof(Index));
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
