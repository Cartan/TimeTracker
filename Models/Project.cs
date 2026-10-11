using System.ComponentModel.DataAnnotations;

namespace TimeTracker.Models;

public class Project
{
    public int Id { get; set; }

    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    public int CustomerId { get; set; }

    public Customer Customer { get; set; } = null!;

    public ICollection<TimeEntry> TimeEntries { get; set; } = new List<TimeEntry>();
}
