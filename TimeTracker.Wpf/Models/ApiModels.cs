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
    DateTimeOffset StartTime,
    DateTimeOffset? EndTime,
    double DurationMinutes)
{
    public DateTimeOffset StartTimeLocal => StartTime.ToLocalTime();

    public DateTimeOffset? EndTimeLocal => EndTime?.ToLocalTime();
}

public sealed record CreateCustomerRequest(string Name);

public sealed record CreateProjectRequest(string Name, int CustomerId);

public sealed record StartTimeEntryRequest(int ProjectId);
