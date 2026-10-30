namespace TimeTracker.Wpf.Models;

public sealed record CustomerDto(int Id, string Name);

public sealed record ProjectDto(int Id, int CustomerId, string CustomerName, string Name)
{
    public string DisplayName => $"{CustomerName} - {Name}";
}

public sealed record TimeEntryDto(
    int Id,
    int ProjectId,
    string ProjectName,
    DateTime StartTime,
    DateTime? EndTime,
    double DurationMinutes);

public sealed record CreateCustomerRequest(string Name);

public sealed record CreateProjectRequest(string Name, int CustomerId);

public sealed record StartTimeEntryRequest(int ProjectId);
