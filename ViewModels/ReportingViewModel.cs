namespace TimeTracker.ViewModels;

public class ReportingViewModel
{
    public IReadOnlyList<ProjectHoursViewModel> Projects { get; init; } = [];

    public decimal TotalHours { get; init; }
}

public record ProjectHoursViewModel(string CustomerName, string ProjectName, decimal Hours);
