namespace monprojet.NET.Models.Dashboard;

public record DeveloperDashboardViewModel(
    IReadOnlyList<DeveloperTaskItemViewModel> MyTasks,
    IReadOnlyList<NotificationItemViewModel> Notifications,
    IReadOnlyList<TeamMemberViewModel> MyTeam,
    double ScorePercentage);

public record DeveloperTaskItemViewModel(
    int TaskId,
    int ProjectId,
    string Title,
    string ProjectName,
    DateTime Deadline,
    string Priority,
    string Status);

public record NotificationItemViewModel(
    int NotificationId,
    string Message,
    DateTime CreatedAt);

public record TeamMemberViewModel(
    string FullName,
    int ActiveTaskCount,
    IReadOnlyList<TeamMemberTaskViewModel> Tasks);

public record TeamMemberTaskViewModel(
    string Title,
    string ProjectName,
    string Status);
