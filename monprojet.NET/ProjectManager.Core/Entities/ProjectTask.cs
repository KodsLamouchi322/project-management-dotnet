using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ProjectManager.Core.Enums;
using TaskStatusEnum = ProjectManager.Core.Enums.TaskStatus;

namespace ProjectManager.Core.Entities;

public class ProjectTask
{
    [Key]
    public int Id { get; set; }

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
    public TimeSpan Duration { get; set; }

    [Required]
    public TaskStatusEnum Status { get; set; }

    [Range(1, 5)]
    public int? ClientRating { get; set; }

    [Required]
    public int ProjectId { get; set; }

    [ForeignKey(nameof(ProjectId))]
    public Project? Project { get; set; }

    [Required]
    public string DeveloperId { get; set; } = string.Empty;

    [ForeignKey(nameof(DeveloperId))]
    public ApplicationUser? Developer { get; set; }

    [Required]
    public DateTime AssignedDate { get; set; }

    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}
