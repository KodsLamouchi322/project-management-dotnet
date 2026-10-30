using ProjectManager.Core.Enums;
using ProjectTaskStatus = ProjectManager.Core.Enums.TaskStatus;

namespace monprojet.NET.Models.Project;

public record ProjectTeamViewModel(
    int ProjectId,
    string ProjectName,
    TeamRoleCardViewModel SeniorDev,
    TeamRoleCardViewModel Client,
    IReadOnlyList<ProjectTeamDeveloperViewModel> Developers);

public record TeamRoleCardViewModel(
    string FullName,
    string Role,
    string Initials);

public record ProjectTeamDeveloperViewModel(
    string DeveloperId,
    string FullName,
    string Initials,
    double MonthlyScorePercentage,
    IReadOnlyList<ProjectTeamTaskItemViewModel> Tasks);

public record ProjectTeamTaskItemViewModel(
    string Title,
    TaskPriority Priority,
    ProjectTaskStatus Status,
    DateTime Deadline);
