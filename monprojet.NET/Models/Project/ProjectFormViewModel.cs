using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace monprojet.NET.Models.Project;

public class ProjectFormViewModel : IValidatableObject
{
    public int? Id { get; set; }

    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(2000)]
    public string Description { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Date)]
    public DateTime StartDate { get; set; }

    [Required]
    [DataType(DataType.Date)]
    public DateTime Deadline { get; set; }

    [Required]
    public string ClientId { get; set; } = string.Empty;

    public IEnumerable<SelectListItem> Clients { get; set; } = Enumerable.Empty<SelectListItem>();

    public IEnumerable<SelectListItem> Developers { get; set; } = Enumerable.Empty<SelectListItem>();

    public IReadOnlyList<ProjectTaskEditListItemViewModel> Tasks { get; set; } = Array.Empty<ProjectTaskEditListItemViewModel>();

    /// <summary>Represents task rows when re-rendering the create form after a validation error.</summary>
    public List<CreateTaskViewModel> PendingTasks { get; set; } = new();

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var today = DateTime.Today;

        if (Deadline.Date < StartDate.Date)
        {
            yield return new ValidationResult(
                "Project deadline must be on or after the start date.",
                new[] { nameof(Deadline), nameof(StartDate) });
        }

        if (Id is null)
        {
            if (StartDate.Date < today)
            {
                yield return new ValidationResult(
                    "Start date cannot be in the past.",
                    new[] { nameof(StartDate) });
            }

            if (Deadline.Date < today)
            {
                yield return new ValidationResult(
                    "Project deadline cannot be in the past.",
                    new[] { nameof(Deadline) });
            }
        }
    }
}

public record ProjectTaskEditListItemViewModel(
    int TaskId,
    string Title,
    string DeveloperName,
    string Status,
    DateTime Deadline);
