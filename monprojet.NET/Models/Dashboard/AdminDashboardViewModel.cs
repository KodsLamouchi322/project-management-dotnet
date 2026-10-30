using ProjectManager.Core.Enums;

namespace monprojet.NET.Models.Dashboard;

public record AdminDashboardViewModel(
    int TotalProjects,
    int ActiveProjects,
    int TotalTasks,
    int CompletedTasks,
    int SeniorDevCount,
    int DeveloperCount,
    IReadOnlyList<AdminProjectSummaryViewModel> Projects,
    IReadOnlyList<AdminTaskSummaryViewModel> RecentTasks,
    IReadOnlyList<AdminTeamMemberViewModel> TeamMembers,
    string TaskStatusChartJson,
    string ProjectStatusChartJson,
    string ProgressTrendChartJson);

public record AdminProjectSummaryViewModel(
    int ProjectId,
    string Name,
    ProjectStatus Status,
    double ProgressPercentage,
    int TotalTasks,
    int CompletedTasks,
    DateTime Deadline,
    string SeniorDevName,
    string ClientName);

public record AdminTaskSummaryViewModel(
    string Title,
    string ProjectName,
    string DeveloperName,
    string Status,
    DateTime Deadline);

public record AdminTeamMemberViewModel(
    string FullName,
    string Email,
    string RoleLabel);
