using System.IO.Compression;
using System.Xml.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TimeTracker.Controllers;
using TimeTracker.Data;
using TimeTracker.Models;
using TimeTracker.Services;
using TimeTracker.ViewModels;
using Xunit;

namespace TimeTracker.Tests.Controllers;

public class ReportingControllerTests
{
    [Fact]
    public async Task Index_GroupsTrackedHoursByCustomerAndProject_IncludingProjectsWithoutEntries()
    {
        var db = CreateDbContext();
        var customer = new Customer { Name = "Contoso" };
        var trackedProject = new Project { Name = "Portal", Customer = customer };
        var emptyProject = new Project { Name = "New project", Customer = customer };
        db.Projects.AddRange(trackedProject, emptyProject);
        await db.SaveChangesAsync();

        var start = DateTime.UtcNow.AddHours(-2);
        db.TimeEntries.Add(new TimeEntry
        {
            ProjectId = trackedProject.Id,
            StartTime = start,
            EndTime = start.AddHours(1.5)
        });
        await db.SaveChangesAsync();

        var controller = new ReportingController(db);
        var result = Assert.IsType<ViewResult>(await controller.Index());
        var report = Assert.IsType<ReportingViewModel>(result.Model);

        Assert.Equal(2, report.Projects.Count);
        Assert.Equal(new ProjectHoursViewModel("Contoso", "New project", 0m), report.Projects[0]);
        Assert.Equal(new ProjectHoursViewModel("Contoso", "Portal", 1.5m), report.Projects[1]);
        Assert.Equal(1.5m, report.TotalHours);
    }

    [Fact]
    public void ExcelExport_CreatesWorkbookWithReportRowsAndTotal()
    {
        var report = new ReportingViewModel
        {
            Projects =
            [
                new ProjectHoursViewModel("Contoso", "Portal", 1.5m)
            ],
            TotalHours = 1.5m
        };

        using var stream = new MemoryStream(ReportingExcelExporter.Export(report));
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

        Assert.NotNull(archive.GetEntry("[Content_Types].xml"));
        var worksheet = archive.GetEntry("xl/worksheets/sheet1.xml");
        Assert.NotNull(worksheet);

        using var worksheetStream = worksheet!.Open();
        var document = XDocument.Load(worksheetStream);
        XNamespace spreadsheet = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var cells = document.Descendants(spreadsheet + "c").ToList();

        Assert.Contains(cells, cell => (string?)cell.Element(spreadsheet + "is")?.Element(spreadsheet + "t") == "Contoso");
        Assert.Contains(cells, cell => (string?)cell.Element(spreadsheet + "is")?.Element(spreadsheet + "t") == "Portal");
        Assert.Contains(cells, cell => (string?)cell.Element(spreadsheet + "v") == "1.5");
        Assert.Contains(cells, cell => (string?)cell.Element(spreadsheet + "is")?.Element(spreadsheet + "t") == "Total");
    }

    private static TimeTrackerDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<TimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new TimeTrackerDbContext(options);
    }
}
