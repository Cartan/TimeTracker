using System.ComponentModel.DataAnnotations;

namespace TimeTracker.Dtos;

public record CustomerDto(int Id, string Name);

public record ProjectDto(int Id, int CustomerId, string CustomerName, string Name);

public record TimeEntryDto(
    int Id,
    int ProjectId,
    string ProjectName,
    DateTimeOffset StartTime,
    DateTimeOffset? EndTime,
    double DurationMinutes);

public record CreateCustomerRequest(
    [Required, StringLength(200)] string Name);

public record CreateProjectRequest(
    [Required, StringLength(200)] string Name,
    [Range(1, int.MaxValue)] int CustomerId);

public record StartTimeEntryRequest([Range(1, int.MaxValue)] int ProjectId);
