using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using monprojet.NET.Models.Dashboard;
using ProjectManager.Core.Entities;
using ProjectManager.Core.Enums;
using ProjectManager.Infrastructure.Data;
using ProjectManager.Infrastructure.Services;
using ProjectTaskStatus = ProjectManager.Core.Enums.TaskStatus;
using System.Security.Claims;
using System.Text.Json;
using System.Globalization;

namespace monprojet.NET.Controllers;

public class DashboardController : Controller
{
    private readonly ApplicationDbContext _dbContext;
    private readonly TaskService _taskService;
    private readonly ProjectService _projectService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly INotificationService _notificationService;

    public DashboardController(
        ApplicationDbContext dbContext,
        TaskService taskService,
        ProjectService projectService,
        UserManager<ApplicationUser> userManager,
        INotificationService notificationService)
    {
        _dbContext = dbContext;
        _taskService = taskService;
        _projectService = projectService;
        _userManager = userManager;
        _notificationService = notificationService;
    }

    /// <summary>
    /// Affiche le tableau de bord du client.
    /// Protégé par l'attribut [Authorize(Roles = "Client")], garantissant qu'aucun autre rôle ne peut y accéder.
    /// </summary>
    [HttpGet]
    [Authorize(Roles = nameof(UserRole.Client))]
    public async Task<IActionResult> Client()
    {
        var clientId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(clientId))
        {
            return Forbid();
        }

        var projects = await _dbContext.Projects
            .Include(p => p.Tasks)
            .Where(p => !p.IsDeleted && p.ClientId == clientId)
            .OrderByDescending(p => p.StartDate)
            .ToListAsync();

        var model = projects.Select(project =>
        {
            var totalTasks = project.Tasks.Count;
            var completedTasks = project.Tasks.Count(task => task.Status == ProjectTaskStatus.Completed);
            var progress = totalTasks == 0 ? 0d : (double)completedTasks / totalTasks * 100d;

            return new ProjectDashboardViewModel
            {
                ProjectId = project.Id,
                Name = project.Name,
                Description = project.Description,
                Status = project.Status,
                TotalTasks = totalTasks,
                CompletedTasks = completedTasks,
                ProgressPercentage = Math.Round(progress, 2)
            };
        }).ToList();

        return View(model);
    }

    /// <summary>
    /// Tableau de bord de la direction (Admin). 
    /// Agglomère d'énormes quantités de données pour dessiner des graphiques (via Chart.js).
    /// </summary>
    [HttpGet]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<IActionResult> Admin()
    {
        var progressByProject = await _projectService.UpdateProjectStatus();

        var projects = await _dbContext.Projects
            .Include(project => project.Tasks)
            .Include(project => project.SeniorDev)
            .Include(project => project.Client)
            .Where(project => !project.IsDeleted)
            .OrderByDescending(project => project.StartDate)
            .ToListAsync();

        var projectItems = projects.Select(project =>
        {
            var totalTasks = project.Tasks.Count;
            var completedTasks = project.Tasks.Count(task => task.Status == ProjectTaskStatus.Completed);

            return new AdminProjectSummaryViewModel(
                project.Id,
                project.Name,
                project.Status,
                progressByProject.TryGetValue(project.Id, out var progress) ? progress : 0d,
                totalTasks,
                completedTasks,
                project.Deadline,
                project.SeniorDev?.FullName ?? "—",
                project.Client?.FullName ?? "—");
        }).ToList();

        var allTasks = projects.SelectMany(project => project.Tasks).ToList();
        var toDoTaskTotal = allTasks.Count(task => task.Status == ProjectTaskStatus.ToDo);
        var inProgressTaskTotal = allTasks.Count(task => task.Status == ProjectTaskStatus.InProgress);
        var completedTaskTotal = allTasks.Count(task => task.Status == ProjectTaskStatus.Completed);

        var taskStatusChartJson = JsonSerializer.Serialize(new
        {
            labels = new[] { "To Do", "In Progress", "Completed" },
            datasets = new[]
            {
                new
                {
                    data = new[] { toDoTaskTotal, inProgressTaskTotal, completedTaskTotal },
                    backgroundColor = new[] { "#6c757d", "#fd7e14", "#198754" }
                }
            }
        });

        var inProgressProjects = projects.Count(project => project.Status == ProjectStatus.InProgress);
        var completedProjects = projects.Count(project => project.Status == ProjectStatus.Completed);
        var lateProjectsCount = projects.Count(project => project.Status == ProjectStatus.Late);

        var projectStatusChartJson = JsonSerializer.Serialize(new
        {
            labels = new[] { "In progress", "Completed", "Late" },
            datasets = new[]
            {
                new
                {
                    label = "Projects",
                    data = new[] { inProgressProjects, completedProjects, lateProjectsCount },
                    backgroundColor = new[] { "#0d6efd", "#198754", "#dc3545" }
                }
            }
        });

        var projectIds = projects.Select(project => project.Id).ToHashSet();
        var trendStart = DateTime.Today.AddDays(-45);
        var snapshots = await _dbContext.DailySnapshots
            .AsNoTracking()
            .Where(snapshot => snapshot.Date >= trendStart && projectIds.Contains(snapshot.ProjectId))
            .ToListAsync();

        // GroupBy() pour grouper l'historique par jour.
        // C'est ce qui crée la courbe "Average progress %" de la plateforme.
        var progressByDay = snapshots
            .GroupBy(snapshot => snapshot.Date.Date)
            .OrderBy(group => group.Key)
            .Select(group => new
            {
                Label = group.Key.ToString("dd MMM", CultureInfo.InvariantCulture),
                Value = Math.Round(group.Average(snapshot => snapshot.ProgressPercent), 2)
            })
            .ToList();

        var progressTrendChartJson = JsonSerializer.Serialize(new
        {
            labels = progressByDay.Select(point => point.Label).ToArray(),
            datasets = new[]
            {
                new
                {
                    label = "Average progress %",
                    data = progressByDay.Select(point => point.Value).ToArray(),
                    tension = 0.35,
                    fill = true
                }
            }
        });

        var recentTasks = await _dbContext.ProjectTasks
            .AsNoTracking()
            .Include(task => task.Project)
            .Include(task => task.Developer)
            .Where(task => task.Project != null && !task.Project.IsDeleted)
            .OrderBy(task => task.Deadline)
            .Take(30)
            .Select(task => new AdminTaskSummaryViewModel(
                task.Title,
                task.Project!.Name,
                task.Developer != null ? task.Developer.FullName : "—",
                task.Status.ToString(),
                task.Deadline))
            .ToListAsync();

        var seniorDevelopers = await _userManager.GetUsersInRoleAsync(nameof(UserRole.SeniorDev));
        var developers = await _userManager.GetUsersInRoleAsync(nameof(UserRole.Developer));

        var teamMembers = new List<AdminTeamMemberViewModel>();
        foreach (var user in seniorDevelopers.OrderBy(user => user.FullName))
        {
            teamMembers.Add(new AdminTeamMemberViewModel(
                string.IsNullOrWhiteSpace(user.FullName) ? user.Email ?? user.UserName ?? "—" : user.FullName,
                user.Email ?? user.UserName ?? "—",
                "Senior Developer"));
        }

        foreach (var user in developers.OrderBy(user => user.FullName))
        {
            teamMembers.Add(new AdminTeamMemberViewModel(
                string.IsNullOrWhiteSpace(user.FullName) ? user.Email ?? user.UserName ?? "—" : user.FullName,
                user.Email ?? user.UserName ?? "—",
                "Developer"));
        }

        var model = new AdminDashboardViewModel(
            projects.Count,
            projects.Count(project => project.Status != ProjectStatus.Completed),
            allTasks.Count,
            completedTaskTotal,
            seniorDevelopers.Count,
            developers.Count,
            projectItems,
            recentTasks,
            teamMembers,
            taskStatusChartJson,
            projectStatusChartJson,
            progressTrendChartJson);

        return View(model);
    }

    /// <summary>
    /// Displays monthly score history and selected month task breakdown for the current developer.
    /// </summary>
    [HttpGet]
    [Authorize(Roles = nameof(UserRole.Developer))]
    public async Task<IActionResult> ScoreHistory(string? month)
    {
        var developerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(developerId))
        {
            return Forbid();
        }

        var now = DateTime.Now;
        var currentMonthStart = new DateTime(now.Year, now.Month, 1);
        var monthStarts = Enumerable.Range(0, 6)
            .Select(index => currentMonthStart.AddMonths(-index))
            .OrderBy(date => date)
            .ToList();

        var monthlyScores = new List<MonthlyScoreItemViewModel>(monthStarts.Count);
        foreach (var monthStart in monthStarts)
        {
            var score = await _taskService.CalculateDeveloperScore(developerId, monthStart.Month, monthStart.Year);

            var assignedCount = await _dbContext.ProjectTasks.CountAsync(task =>
                task.DeveloperId == developerId
                && task.AssignedDate.Month == monthStart.Month
                && task.AssignedDate.Year == monthStart.Year);

            var completedCount = await _dbContext.ProjectTasks.CountAsync(task =>
                task.DeveloperId == developerId
                && task.AssignedDate.Month == monthStart.Month
                && task.AssignedDate.Year == monthStart.Year
                && task.Status == ProjectTaskStatus.Completed);

            monthlyScores.Add(new MonthlyScoreItemViewModel(
                monthStart.ToString("MMM yyyy", CultureInfo.InvariantCulture),
                monthStart.Year,
                monthStart.Month,
                assignedCount,
                completedCount,
                Math.Round(score * 100d, 2)));
        }

        var monthOptions = monthStarts
            .Select(monthStart => new MonthOptionViewModel(
                monthStart.ToString("yyyy-MM", CultureInfo.InvariantCulture),
                monthStart.ToString("MMMM yyyy", CultureInfo.InvariantCulture)))
            .ToList();

        var selectedMonthValue = month;
        if (string.IsNullOrWhiteSpace(selectedMonthValue) || !monthOptions.Any(option => option.Value == selectedMonthValue))
        {
            selectedMonthValue = monthOptions.Last().Value;
        }

        var selectedMonthStart = DateTime.ParseExact(selectedMonthValue, "yyyy-MM", CultureInfo.InvariantCulture);

        var selectedMonthTasks = await _dbContext.ProjectTasks
            .Include(task => task.Project)
            .Where(task => task.DeveloperId == developerId
                           && task.AssignedDate.Month == selectedMonthStart.Month
                           && task.AssignedDate.Year == selectedMonthStart.Year)
            .OrderBy(task => task.Deadline)
            .Select(task => new ScoreHistoryTaskItemViewModel(
                task.Title,
                task.Project != null ? task.Project.Name : "-",
                task.Status.ToString(),
                task.Priority.ToString(),
                task.Deadline))
            .ToListAsync();

        var lineChartDataJson = JsonSerializer.Serialize(new
        {
            labels = monthlyScores.Select(item => item.MonthLabel).ToArray(),
            datasets = new[]
            {
                new
                {
                    label = "Score %",
                    data = monthlyScores.Select(item => item.ScorePercentage).ToArray(),
                    borderColor = "#0d6efd",
                    backgroundColor = "rgba(13, 110, 253, 0.2)",
                    tension = 0.35,
                    fill = true
                }
            }
        });

        var currentMonthScore = monthlyScores.Last().ScorePercentage;

        var model = new ScoreHistoryViewModel(
            monthlyScores,
            monthOptions,
            selectedMonthValue,
            selectedMonthTasks,
            lineChartDataJson,
            currentMonthScore);

        return View(model);
    }

    /// <summary>
    /// Displays the senior developer dashboard with assigned projects, alerts, and top developers.
    /// </summary>
    [HttpGet]
    [Authorize(Roles = nameof(UserRole.SeniorDev))]
    public async Task<IActionResult> SeniorDev()
    {
        var seniorDevId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(seniorDevId))
        {
            return Forbid();
        }

        var progressByProject = await _projectService.UpdateProjectStatus(seniorDevId);

        var projects = await _dbContext.Projects
            .Include(project => project.Tasks)
            .Where(project => !project.IsDeleted && project.SeniorDevId == seniorDevId)
            .OrderBy(project => project.Deadline)
            .ToListAsync();

        var projectItems = projects.Select(project =>
        {
            var totalTasks = project.Tasks.Count;
            var completedTasks = project.Tasks.Count(task => task.Status == ProjectTaskStatus.Completed);

            return new SeniorDevProjectItemViewModel(
                project.Id,
                project.Name,
                project.Status,
                progressByProject.TryGetValue(project.Id, out var progress) ? progress : 0d,
                totalTasks,
                completedTasks,
                project.Deadline);
        }).ToList();

        var today = DateTime.Today;
        var upcomingDeadlineLimit = today.AddDays(3);

        var lateProjects = projectItems
            .Where(project => (project.Status == ProjectStatus.Late || project.Deadline.Date < today)
                              && project.Status != ProjectStatus.Completed)
            .Select(project => new SeniorDevAlertItemViewModel(
                project.ProjectId,
                project.Name,
                "Project is late."))
            .ToList();

        var upcomingDeadlineProjects = projectItems
            .Where(project => project.Deadline.Date >= today
                              && project.Deadline.Date <= upcomingDeadlineLimit
                              && project.Status != ProjectStatus.Completed)
            .Select(project => new SeniorDevAlertItemViewModel(
                project.ProjectId,
                project.Name,
                $"Deadline is approaching on {project.Deadline:dd/MM/yyyy}."))
            .ToList();

        var weekStart = DateTime.Today;
        while (weekStart.DayOfWeek != DayOfWeek.Monday)
        {
            weekStart = weekStart.AddDays(-1);
        }

        var weekEnd = weekStart.AddDays(7);
        var developers = await _userManager.GetUsersInRoleAsync(nameof(UserRole.Developer));
        var topDevelopers = new List<TopDeveloperItemViewModel>();

        foreach (var developer in developers)
        {
            var score = await _taskService.CalculateDeveloperWeeklyScore(developer.Id, DateTime.Now);
            var completedTaskCount = await _dbContext.ProjectTasks
                .CountAsync(task => task.DeveloperId == developer.Id
                                    && task.AssignedDate >= weekStart
                                    && task.AssignedDate < weekEnd
                                    && task.Status == ProjectTaskStatus.Completed);

            topDevelopers.Add(new TopDeveloperItemViewModel(
                developer.FullName,
                Math.Round(score * 100d, 2),
                completedTaskCount));
        }

        var leaderboard = topDevelopers
            .OrderByDescending(item => item.ScorePercentage)
            .ThenByDescending(item => item.CompletedTaskCount)
            .Take(5)
            .ToList();

        var allProjectTasks = projects.SelectMany(project => project.Tasks).ToList();
        var toDoTaskTotal = allProjectTasks.Count(task => task.Status == ProjectTaskStatus.ToDo);
        var inProgressTaskTotal = allProjectTasks.Count(task => task.Status == ProjectTaskStatus.InProgress);
        var completedTaskTotal = allProjectTasks.Count(task => task.Status == ProjectTaskStatus.Completed);

        var taskStatusChartJson = JsonSerializer.Serialize(new
        {
            labels = new[] { "To Do", "In Progress", "Completed" },
            datasets = new[]
            {
                new
                {
                    data = new[] { toDoTaskTotal, inProgressTaskTotal, completedTaskTotal },
                    backgroundColor = new[] { "#6c757d", "#fd7e14", "#198754" }
                }
            }
        });

        var topDevelopersChartJson = JsonSerializer.Serialize(new
        {
            labels = leaderboard.Select(item => item.FullName).ToArray(),
            datasets = new[]
            {
                new
                {
                    label = "Weekly Score %",
                    data = leaderboard.Select(item => item.ScorePercentage).ToArray(),
                    backgroundColor = "#0d6efd"
                }
            }
        });

        var activeProjectIds = projects
            .Where(project => project.Status != ProjectStatus.Completed)
            .Select(project => project.Id)
            .ToList();

        var snapshots = await _dbContext.DailySnapshots
            .Where(snapshot => activeProjectIds.Contains(snapshot.ProjectId))
            .OrderBy(snapshot => snapshot.Date)
            .ToListAsync();

        var weeklyPoints = snapshots
            .GroupBy(snapshot => new
            {
                snapshot.ProjectId,
                snapshot.Date.Year,
                Week = ISOWeek.GetWeekOfYear(snapshot.Date)
            })
            .Select(group => new
            {
                group.Key.ProjectId,
                group.Key.Year,
                group.Key.Week,
                Progress = group.OrderByDescending(item => item.Date).First().ProgressPercent
            })
            .ToList();

        var weekLabels = weeklyPoints
            .Select(item => (item.Year, item.Week))
            .Distinct()
            .OrderBy(item => item.Year)
            .ThenBy(item => item.Week)
            .Select(item => $"W{item.Week} {item.Year}")
            .ToList();

        var progressDatasets = projects
            .Where(project => activeProjectIds.Contains(project.Id))
            .Select(project =>
            {
                var pointsByWeek = weeklyPoints
                    .Where(item => item.ProjectId == project.Id)
                    .ToDictionary(item => $"W{item.Week} {item.Year}", item => item.Progress);

                var values = weekLabels
                    .Select(label => pointsByWeek.TryGetValue(label, out var progress) ? progress : (double?)null)
                    .ToArray();

                return new
                {
                    label = project.Name,
                    data = values,
                    tension = 0.35,
                    fill = false
                };
            })
            .ToArray();

        var projectProgressChartJson = JsonSerializer.Serialize(new
        {
            labels = weekLabels,
            datasets = progressDatasets
        });

        var model = new SeniorDevDashboardViewModel(
            projectItems,
            lateProjects,
            upcomingDeadlineProjects,
            leaderboard,
            taskStatusChartJson,
            topDevelopersChartJson,
            projectProgressChartJson);

        return View(model);
    }

    /// <summary>
    /// Displays the developer dashboard with tasks, notifications, score, and team members.
    /// </summary>
    [HttpGet]
    [Authorize(Roles = nameof(UserRole.Developer))]
    public async Task<IActionResult> Developer()
    {
        var developerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(developerId))
        {
            return Forbid();
        }

        var myTasks = await _dbContext.ProjectTasks
            .Include(task => task.Project)
            .Where(task => task.DeveloperId == developerId
                           && task.Project != null
                           && !task.Project.IsDeleted)
            .OrderBy(task => task.Deadline)
            .Select(task => new DeveloperTaskItemViewModel(
                task.Id,
                task.ProjectId,
                task.Title,
                task.Project!.Name,
                task.Deadline,
                task.Priority.ToString(),
                task.Status.ToString()))
            .ToListAsync();

        var unreadNotifications = await _notificationService.GetUnreadAsync(developerId);

        var notifications = unreadNotifications
            .Select(notification => new NotificationItemViewModel(
                notification.Id,
                notification.Message,
                notification.CreatedAt))
            .ToList();

        var myProjectIds = await _dbContext.ProjectTasks
            .Where(task => task.DeveloperId == developerId)
            .Select(task => task.ProjectId)
            .Distinct()
            .ToListAsync();

        var teamTasks = await _dbContext.ProjectTasks
            .Include(task => task.Project)
            .Include(task => task.Developer)
            .Where(task => myProjectIds.Contains(task.ProjectId)
                           && task.DeveloperId != developerId
                           && task.Project != null
                           && !task.Project.IsDeleted)
            .ToListAsync();

        var team = teamTasks
            .Where(task => task.Developer is not null)
            .GroupBy(task => new { task.DeveloperId, task.Developer!.FullName })
            .Select(group => new TeamMemberViewModel(
                group.Key.FullName,
                group.Count(task => task.Status == ProjectTaskStatus.InProgress),
                group
                    .OrderBy(task => task.Deadline)
                    .Select(task => new TeamMemberTaskViewModel(
                        task.Title,
                        task.Project?.Name ?? "-",
                        task.Status.ToString()))
                    .ToList()))
            .OrderBy(member => member.FullName)
            .ToList();

        var score = await _taskService.CalculateDeveloperWeeklyScore(developerId, DateTime.Now);

        var model = new DeveloperDashboardViewModel(
            myTasks,
            notifications,
            team,
            Math.Round(score * 100d, 2));

        return View(model);
    }

    /// <summary>
    /// Marks a developer notification as read.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = nameof(UserRole.Developer))]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkNotificationAsRead(int notificationId)
    {
        var developerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(developerId))
        {
            return Forbid();
        }

        var notification = await _dbContext.Notifications
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == notificationId && item.UserId == developerId);

        if (notification is null)
        {
            return NotFound();
        }

        await _notificationService.MarkAsReadAsync(notificationId);

        return Json(new { success = true });
    }

    /// <summary>
    /// Updates the status of a task assigned to the current developer.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = nameof(UserRole.Developer))]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateTaskStatus(int taskId, string status)
    {
        var developerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(developerId))
        {
            return Forbid();
        }

        if (!Enum.TryParse<ProjectTaskStatus>(status, true, out var parsedStatus))
        {
            return BadRequest(new { success = false, message = "Invalid task status." });
        }

        var task = await _dbContext.ProjectTasks
            .Include(item => item.Project)
            .FirstOrDefaultAsync(item => item.Id == taskId
                                         && item.DeveloperId == developerId
                                         && item.Project != null
                                         && !item.Project.IsDeleted);

        if (task is null)
        {
            return NotFound(new { success = false, message = "Task not found." });
        }

        task.Status = parsedStatus;
        await _dbContext.SaveChangesAsync();

        var score = await _taskService.CalculateDeveloperWeeklyScore(developerId, DateTime.Now);

        return Json(new
        {
            success = true,
            status = task.Status.ToString(),
            scorePercentage = Math.Round(score * 100d, 2)
        });
    }
}
