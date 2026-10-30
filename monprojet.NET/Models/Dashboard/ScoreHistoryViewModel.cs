namespace monprojet.NET.Models.Dashboard;

public record ScoreHistoryViewModel(
    IReadOnlyList<MonthlyScoreItemViewModel> MonthlyScores,
    IReadOnlyList<MonthOptionViewModel> MonthOptions,
    string SelectedMonth,
    IReadOnlyList<ScoreHistoryTaskItemViewModel> SelectedMonthTasks,
    string LineChartDataJson,
    double CurrentMonthScore);

public record MonthlyScoreItemViewModel(
    string MonthLabel,
    int Year,
    int Month,
    int TasksAssigned,
    int TasksCompleted,
    double ScorePercentage);

public record MonthOptionViewModel(
    string Value,
    string Label);

public record ScoreHistoryTaskItemViewModel(
    string Title,
    string ProjectName,
    string Status,
    string Priority,
    DateTime Deadline);
