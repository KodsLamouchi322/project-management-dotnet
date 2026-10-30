using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ProjectManager.Core.Enums;
using ProjectManager.Infrastructure.Data;
using ProjectTaskStatus = ProjectManager.Core.Enums.TaskStatus;

namespace ProjectManager.Infrastructure.Services;

public class NotificationBackgroundService : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(1);

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<NotificationBackgroundService> _logger;

    public NotificationBackgroundService(IServiceProvider serviceProvider, ILogger<NotificationBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <summary>
    /// Runs periodic notification checks for overdue tasks and project deadline alerts.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(CheckInterval);

        await RunChecksAsync(stoppingToken);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunChecksAsync(stoppingToken);
        }
    }

    private async Task RunChecksAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

            var now = DateTime.Now;
            var today = now.Date;
            var nextThreeDays = today.AddDays(3);

            var overdueTasks = await dbContext.ProjectTasks
                .Include(task => task.Project)
                .Where(task => task.Status != ProjectTaskStatus.Completed
                               && task.Deadline < now
                               && task.Project != null
                               && !task.Project.IsDeleted)
                .ToListAsync(cancellationToken);

            foreach (var task in overdueTasks)
            {
                var message = $"Task '{task.Title}' is overdue";
                var exists = await dbContext.Notifications.AnyAsync(
                    notification => notification.UserId == task.DeveloperId
                                    && notification.TaskId == task.Id
                                    && notification.Message == message
                                    && notification.CreatedAt.Date == today,
                    cancellationToken);

                if (!exists)
                {
                    await notificationService.CreateAsync(task.DeveloperId, message, task.Id, cancellationToken);
                }
            }

            var upcomingTaskDeadlines = await dbContext.ProjectTasks
                .Include(task => task.Project)
                .Where(task => task.Status != ProjectTaskStatus.Completed
                               && task.Deadline.Date >= today
                               && task.Deadline.Date <= nextThreeDays
                               && task.Project != null
                               && !task.Project.IsDeleted)
                .ToListAsync(cancellationToken);

            foreach (var task in upcomingTaskDeadlines)
            {
                var daysToDeadline = (task.Deadline.Date - today).Days;
                var message = daysToDeadline == 0
                    ? $"Task '{task.Title}' deadline is today"
                    : $"Task '{task.Title}' deadline in {daysToDeadline} day(s)";

                var exists = await dbContext.Notifications.AnyAsync(
                    notification => notification.UserId == task.DeveloperId
                                    && notification.TaskId == task.Id
                                    && notification.Message == message
                                    && notification.CreatedAt.Date == today,
                    cancellationToken);

                if (!exists)
                {
                    await notificationService.CreateAsync(task.DeveloperId, message, task.Id, cancellationToken);
                }
            }

            var projects = await dbContext.Projects
                .Include(project => project.Tasks)
                .Where(project => !project.IsDeleted)
                .ToListAsync(cancellationToken);

            foreach (var project in projects)
            {
                var totalTasks = project.Tasks.Count;
                var completedTasks = project.Tasks.Count(task => task.Status == ProjectTaskStatus.Completed);
                var isCompleted = totalTasks > 0 && completedTasks == totalTasks;

                if (isCompleted)
                {
                    project.Status = ProjectStatus.Completed;
                    continue;
                }

                var daysToDeadline = (project.Deadline.Date - today).Days;
                if (daysToDeadline is >= 0 and <= 3)
                {
                    var warningMessage = $"Project '{project.Name}' deadline in {daysToDeadline} days";
                    var warningExists = await dbContext.Notifications.AnyAsync(
                        notification => notification.UserId == project.SeniorDevId
                                        && notification.TaskId == null
                                        && notification.Message == warningMessage
                                        && notification.CreatedAt.Date == today,
                        cancellationToken);

                    if (!warningExists)
                    {
                        await notificationService.CreateAsync(project.SeniorDevId, warningMessage, null, cancellationToken);
                    }
                }

                if (project.Deadline < now && project.Status != ProjectStatus.Late)
                {
                    project.Status = ProjectStatus.Late;

                    var lateMessage = $"Project '{project.Name}' is now late";
                    var lateExists = await dbContext.Notifications.AnyAsync(
                        notification => notification.UserId == project.SeniorDevId
                                        && notification.TaskId == null
                                        && notification.Message == lateMessage
                                        && notification.CreatedAt.Date == today,
                        cancellationToken);

                    if (!lateExists)
                    {
                        await notificationService.CreateAsync(project.SeniorDevId, lateMessage, null, cancellationToken);
                    }
                }
                else if (project.Deadline >= now)
                {
                    project.Status = ProjectStatus.InProgress;
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Scheduled notification check failed.");
        }
    }
}
