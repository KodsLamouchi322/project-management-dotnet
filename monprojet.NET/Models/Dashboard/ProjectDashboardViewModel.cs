using ProjectManager.Core.Enums;

namespace monprojet.NET.Models.Dashboard;

public class ProjectDashboardViewModel
{
    public int ProjectId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public ProjectStatus Status { get; set; }

    public double ProgressPercentage { get; set; }

    public int TotalTasks { get; set; }

    public int CompletedTasks { get; set; }
}
