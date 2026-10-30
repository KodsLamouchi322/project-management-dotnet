using System.ComponentModel.DataAnnotations;
using ProjectManager.Core.Enums;

namespace monprojet.NET.Models.Project;

public class CreateTaskViewModel
{
    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [StringLength(2000)]
    public string Description { get; set; } = string.Empty;

    [Required]
    public TaskPriority Priority { get; set; }

    [Required]
    [DataType(DataType.Date)]
    public DateTime Deadline { get; set; }

    [Required]
    [DataType(DataType.Duration)]
    public TimeSpan Duration { get; set; }

    [Required]
    public string DeveloperId { get; set; } = string.Empty;
}
