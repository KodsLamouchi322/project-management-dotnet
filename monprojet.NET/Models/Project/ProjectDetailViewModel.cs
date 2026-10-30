using ProjectManager.Core.Enums;
using ProjectTaskStatus = ProjectManager.Core.Enums.TaskStatus;

namespace monprojet.NET.Models.Project;

public record ProjectDetailViewModel(
    int ProjectId,
    string Name,
    string Description,
    string ClientName,
    string SeniorDevName,
    DateTime StartDate,
    DateTime? EndDate,
    DateTime Deadline,
    ProjectStatus Status,
    double ProgressPercentage,
    int CompletedTasks,
    int TotalTasks,
    int DaysRemaining,
    int? ClientRating,
    bool CanRate,
    IReadOnlyList<ProjectNoteViewModel> Notes,
    IReadOnlyList<ProjectTaskDetailItemViewModel> Tasks);

public record ProjectTaskDetailItemViewModel(
    int TaskId,
    string Title,
    string DeveloperName,
    TaskPriority Priority,
    DateTime Deadline,
    ProjectTaskStatus Status,
    int? ClientRating);

public record ProjectNoteViewModel(
    string AuthorName,
    string Message,
    DateTime CreatedAt);
