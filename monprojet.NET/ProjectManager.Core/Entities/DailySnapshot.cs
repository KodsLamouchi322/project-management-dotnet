using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProjectManager.Core.Entities;

public class DailySnapshot
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int ProjectId { get; set; }

    [ForeignKey(nameof(ProjectId))]
    public Project? Project { get; set; }

    [Required]
    public DateTime Date { get; set; }

    [Required]
    public double ProgressPercent { get; set; }
}
