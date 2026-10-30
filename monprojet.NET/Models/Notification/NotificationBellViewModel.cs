namespace monprojet.NET.Models.Notification;

public record NotificationBellViewModel(
    int UnreadCount,
    IReadOnlyList<NotificationBellItemViewModel> Items,
    bool CanViewNotifications);

public record NotificationBellItemViewModel(
    int NotificationId,
    string Message,
    string TimeAgo);
