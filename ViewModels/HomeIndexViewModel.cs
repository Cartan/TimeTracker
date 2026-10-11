using TimeTracker.Models;

namespace TimeTracker.ViewModels;

public class HomeIndexViewModel
{
    public List<Project> Projects { get; set; } = new();
    public int? LastProjectId { get; set; }
    public TimeEntry? ActiveTimeEntry { get; set; }
}
