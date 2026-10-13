using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TimeTracker.Data;
using TimeTracker.Models;
using TimeTracker.ViewModels;

namespace TimeTracker.Controllers;

public class ProjectsController : Controller
{
    private readonly TimeTrackerDbContext _context;

    public ProjectsController(TimeTrackerDbContext context)
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

        return View(projects);
    }

    public async Task<IActionResult> Create()
    {
        await PopulateCustomersAsync();
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ProjectCreateViewModel project)
    {
        if (ModelState.IsValid &&
            !await _context.Customers.AnyAsync(customer => customer.Id == project.CustomerId))
        {
            ModelState.AddModelError(nameof(project.CustomerId), "Please select a valid customer.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateCustomersAsync(project.CustomerId);
            return View(project);
        }

        _context.Projects.Add(new Project
        {
            Name = project.Name,
            CustomerId = project.CustomerId
        });
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var project = await _context.Projects.FindAsync(id);
        if (project is null)
        {
            return NotFound();
        }

        _context.Projects.Remove(project);
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateCustomersAsync(int? selectedCustomerId = null)
    {
        var customers = await _context.Customers
            .OrderBy(c => c.Name)
            .ToListAsync();

        ViewBag.CustomerId = new SelectList(customers, "Id", "Name", selectedCustomerId);
    }
}
