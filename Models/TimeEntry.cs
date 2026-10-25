namespace TimeTracker.Models;

public class TimeEntry
{
    public int Id { get; set; }

    public int ProjectId { get; set; }

    public Project Project { get; set; } = null!;

    public DateTime StartTime { get; set; }

    public DateTime? EndTime { get; set; }

    public int? UserId { get; set; }

    public User? User { get; set; }

    public TimeSpan Duration => (EndTime ?? DateTime.UtcNow) - StartTime;
}
