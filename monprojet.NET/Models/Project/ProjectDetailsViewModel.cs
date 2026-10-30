using ProjectManager.Core.Entities;
using ProjectEntity = ProjectManager.Core.Entities.Project;

namespace monprojet.NET.Models.Project;

public class ProjectDetailsViewModel
{
    public ProjectEntity Project { get; set; } = null!;

    public double ProgressPercentage { get; set; }

    public int TaskCount { get; set; }

    public int CompletedTaskCount { get; set; }
}
