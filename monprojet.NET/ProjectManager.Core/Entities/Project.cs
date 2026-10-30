using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ProjectManager.Core.Enums;

namespace ProjectManager.Core.Entities;

public class Project
{
    [Key]
    public int Id { get; set; }

    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(2000)]
    public string Description { get; set; } = string.Empty;

    [Required]
    public DateTime StartDate { get; set; }

    /// <summary>Completion date; set when the project reaches 100% progress.</summary>
    public DateTime? EndDate { get; set; }

    [Required]
    public DateTime Deadline { get; set; }

    [Required]
    public ProjectStatus Status { get; set; }

    [Required]
    public bool IsDeleted { get; set; }

    [Range(1, 5)]
    public int? ClientRating { get; set; }

    public int LastProgressNotificationPercent { get; set; }

    [Required]
    public string ClientId { get; set; } = string.Empty;

    [ForeignKey(nameof(ClientId))]
    public ApplicationUser? Client { get; set; }

    [Required]
    public string SeniorDevId { get; set; } = string.Empty;

    [ForeignKey(nameof(SeniorDevId))]
    public ApplicationUser? SeniorDev { get; set; }

    public ICollection<ProjectTask> Tasks { get; set; } = new List<ProjectTask>();

    public ICollection<DailySnapshot> DailySnapshots { get; set; } = new List<DailySnapshot>();

    public ICollection<ProjectNote> Notes { get; set; } = new List<ProjectNote>();
}
