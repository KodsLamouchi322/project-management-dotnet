using ProjectManager.Core.Entities;

namespace ProjectManager.Infrastructure.Services;

public interface INotificationService
{
    /// <summary>
    /// Creates a notification for a user.
    /// </summary>
    Task<Notification> CreateAsync(string userId, string message, int? taskId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns unread notifications for a user.
    /// </summary>
    Task<IReadOnlyList<Notification>> GetUnreadAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks one notification as read.
    /// </summary>
    Task MarkAsReadAsync(int notificationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks all notifications as read for a user.
    /// </summary>
    Task MarkAllAsReadAsync(string userId, CancellationToken cancellationToken = default);
}
