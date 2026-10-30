using System.ComponentModel.DataAnnotations;

namespace TimeTracker.Dtos;

public record CustomerDto(int Id, string Name);

public record ProjectDto(int Id, int CustomerId, string CustomerName, string Name);

public record TimeEntryDto(int Id, int ProjectId, string ProjectName, DateTime StartTime, DateTime? EndTime, double DurationMinutes);

public record StartTimeEntryRequest([property: Range(1, int.MaxValue)] int ProjectId);
