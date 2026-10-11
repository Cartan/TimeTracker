using System.ComponentModel.DataAnnotations;

namespace TimeTracker.Models;

public class Customer
{
    public int Id { get; set; }

    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    public ICollection<Project> Projects { get; set; } = new List<Project>();
}
