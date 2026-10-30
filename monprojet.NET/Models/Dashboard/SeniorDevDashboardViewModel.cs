using ProjectManager.Core.Enums;

namespace monprojet.NET.Models.Dashboard;

public record SeniorDevDashboardViewModel(
    IReadOnlyList<SeniorDevProjectItemViewModel> Projects,
    IReadOnlyList<SeniorDevAlertItemViewModel> LateProjects,
    IReadOnlyList<SeniorDevAlertItemViewModel> UpcomingDeadlineProjects,
    IReadOnlyList<TopDeveloperItemViewModel> TopDevelopers,
    string TaskStatusChartJson,
    string TopDevelopersChartJson,
    string ProjectProgressChartJson);

public record SeniorDevProjectItemViewModel(
    int ProjectId,
    string Name,
    ProjectStatus Status,
    double ProgressPercentage,
    int TotalTasks,
    int CompletedTasks,
    DateTime Deadline);

public record SeniorDevAlertItemViewModel(
    int ProjectId,
    string ProjectName,
    string Message);

public record TopDeveloperItemViewModel(
    string FullName,
    double ScorePercentage,
    int CompletedTaskCount);
