using ProjectManager.Core.Enums;

namespace monprojet.NET.Models.Project;

public class ProjectListItemViewModel
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public ProjectStatus Status { get; set; }

    public double ProgressPercentage { get; set; }

    public int TaskCount { get; set; }
}
