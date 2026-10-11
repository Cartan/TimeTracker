using System.ComponentModel.DataAnnotations;

namespace TimeTracker.ViewModels;

public class ProjectCreateViewModel
{
    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int CustomerId { get; set; }
}
