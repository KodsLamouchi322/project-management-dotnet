using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using ProjectManager.Core.Enums;

namespace monprojet.NET.Models.TaskManagement;

public class TaskFormViewModel : IValidatableObject
{
    public int? Id { get; set; }

    [Required]
    public int ProjectId { get; set; }

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
    [DataType(DataType.Date)]
    public DateTime ProjectStartDate { get; set; }

    [Required]
    [DataType(DataType.Date)]
    public DateTime ProjectDeadline { get; set; }

    [Required]
    [DataType(DataType.Duration)]
    public TimeSpan Duration { get; set; }

    [Required]
    public string DeveloperId { get; set; } = string.Empty;

    public IEnumerable<SelectListItem> Developers { get; set; } = Enumerable.Empty<SelectListItem>();

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Deadline.Date <= ProjectStartDate.Date)
        {
            yield return new ValidationResult(
                "Task deadline must be after the project start date (at least 1 day after).",
                new[] { nameof(Deadline) });
        }

        if (Deadline.Date > ProjectDeadline.Date)
        {
            yield return new ValidationResult(
                "Task deadline cannot be after the project deadline.",
                new[] { nameof(Deadline) });
        }
    }
}
