using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TimeTracker.Data;
using TimeTracker.Services;
using TimeTracker.ViewModels;

namespace TimeTracker.Controllers;

public class ReportingController : Controller
{
    private readonly TimeTrackerDbContext _context;

    public ReportingController(TimeTrackerDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        return View(await BuildReportAsync());
    }

    public async Task<IActionResult> Excel()
    {
        var report = await BuildReportAsync();
        var content = ReportingExcelExporter.Export(report);
        var fileName = $"time-report-{DateTime.UtcNow:yyyy-MM-dd}.xlsx";

        return File(
            content,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName);
    }

    private async Task<ReportingViewModel> BuildReportAsync()
    {
        var projects = await _context.Projects
            .OrderBy(p => p.Customer.Name)
            .ThenBy(p => p.Name)
            .Select(p => new
            {
                p.Id,
                CustomerName = p.Customer.Name,
                ProjectName = p.Name
            })
            .ToListAsync();

        var entries = await _context.TimeEntries
            .Select(t => new { t.ProjectId, t.StartTime, t.EndTime })
            .ToListAsync();

        var now = DateTime.UtcNow;
        var secondsByProject = entries
            .GroupBy(entry => entry.ProjectId)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(entry => (decimal)((entry.EndTime ?? now) - entry.StartTime).TotalSeconds));

        var rows = projects
            .Select(project =>
            {
                var seconds = secondsByProject.GetValueOrDefault(project.Id);
                var hours = Math.Round(seconds / 3600m, 2, MidpointRounding.AwayFromZero);
                return new ProjectHoursViewModel(project.CustomerName, project.ProjectName, hours);
            })
            .ToList();

        return new ReportingViewModel
        {
            Projects = rows,
            TotalHours = rows.Sum(row => row.Hours)
        };
    }
}
