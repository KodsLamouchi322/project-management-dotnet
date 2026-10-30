using Microsoft.EntityFrameworkCore;
using ProjectManager.Core.Entities;
using ProjectManager.Infrastructure.Data;

namespace ProjectManager.Infrastructure.Services;

public class NotificationService : INotificationService
{
    private readonly ApplicationDbContext _dbContext;

    public NotificationService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Creates a notification for a user.
    /// </summary>
    public async Task<Notification> CreateAsync(string userId, string message, int? taskId = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("User id is required.", nameof(userId));
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Message is required.", nameof(message));
        }

        var notification = new Notification
        {
            UserId = userId,
            Message = message,
            TaskId = taskId,
            IsRead = false,
            CreatedAt = DateTime.Now
        };

        _dbContext.Notifications.Add(notification);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return notification;
    }

    /// <summary>
    /// Returns unread notifications for a user.
    /// </summary>
    public async Task<IReadOnlyList<Notification>> GetUnreadAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("User id is required.", nameof(userId));
        }

        var notifications = await _dbContext.Notifications
            .Where(item => item.UserId == userId && !item.IsRead)
            .OrderByDescending(item => item.CreatedAt)
            .ToListAsync(cancellationToken);

        return notifications;
    }

    /// <summary>
    /// Marks one notification as read.
    /// </summary>
    public async Task MarkAsReadAsync(int notificationId, CancellationToken cancellationToken = default)
    {
        var notification = await _dbContext.Notifications
            .FirstOrDefaultAsync(item => item.Id == notificationId, cancellationToken);

        if (notification is null)
        {
            throw new InvalidOperationException("Notification not found.");
        }

        notification.IsRead = true;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Marks all notifications as read for a user.
    /// </summary>
    public async Task MarkAllAsReadAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("User id is required.", nameof(userId));
        }

        var notifications = await _dbContext.Notifications
            .Where(item => item.UserId == userId && !item.IsRead)
            .ToListAsync(cancellationToken);

        if (notifications.Count == 0)
        {
            return;
        }

        foreach (var notification in notifications)
        {
            notification.IsRead = true;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
