using System.ComponentModel.DataAnnotations;

namespace TimeTracker.Dtos;

public record CustomerDto(int Id, string Name);

public record ProjectDto(int Id, int CustomerId, string CustomerName, string Name);

public record TimeEntryDto(int Id, int ProjectId, string ProjectName, DateTime StartTime, DateTime? EndTime, double DurationMinutes);

public record CreateCustomerRequest(
    [property: Required, StringLength(200)] string Name);

public record CreateProjectRequest(
    [property: Required, StringLength(200)] string Name,
    [property: Range(1, int.MaxValue)] int CustomerId);

public record StartTimeEntryRequest([property: Range(1, int.MaxValue)] int ProjectId);
