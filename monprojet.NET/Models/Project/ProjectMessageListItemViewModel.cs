using ProjectManager.Core.Enums;

namespace monprojet.NET.Models.Project;

public record ProjectMessageListItemViewModel(
    int ProjectId,
    string ProjectName,
    ProjectStatus Status,
    DateTime? LastMessageAt,
    string? LastMessage);
