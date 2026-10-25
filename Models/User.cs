using System.ComponentModel.DataAnnotations;

namespace TimeTracker.Models;

public class User
{
    public int Id { get; set; }

    [Required]
    [StringLength(320)]
    public string Login { get; set; } = string.Empty;
}
