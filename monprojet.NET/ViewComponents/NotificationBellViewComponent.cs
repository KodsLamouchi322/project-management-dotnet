using Microsoft.AspNetCore.Mvc;
using monprojet.NET.Models.Notification;
using ProjectManager.Core.Enums;
using ProjectManager.Infrastructure.Services;
using System.Security.Claims;

namespace monprojet.NET.ViewComponents;

public class NotificationBellViewComponent : ViewComponent
{
    private readonly INotificationService _notificationService;

    public NotificationBellViewComponent(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    /// <summary>
    /// Renders the notification bell with unread count and latest unread notifications.
    /// </summary>
    public async Task<IViewComponentResult> InvokeAsync()
    {
        var user = HttpContext.User;
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);

        var canViewNotifications = user.IsInRole(nameof(UserRole.Developer))
            || user.IsInRole(nameof(UserRole.SeniorDev))
            || user.IsInRole(nameof(UserRole.Client));
        if (!canViewNotifications || string.IsNullOrWhiteSpace(userId))
        {
            return View(new NotificationBellViewModel(0, Array.Empty<NotificationBellItemViewModel>(), false));
        }

        var unreadNotifications = await _notificationService.GetUnreadAsync(userId);

        var items = unreadNotifications
            .Take(5)
            .Select(notification => new NotificationBellItemViewModel(
                notification.Id,
                notification.Message,
                GetTimeAgo(notification.CreatedAt)))
            .ToList();

        var model = new NotificationBellViewModel(unreadNotifications.Count, items, true);
        return View(model);
    }

    private static string GetTimeAgo(DateTime createdAt)
    {
        var elapsed = DateTime.Now - createdAt;

        if (elapsed.TotalMinutes < 1)
        {
            return "just now";
        }

        if (elapsed.TotalHours < 1)
        {
            return $"{Math.Floor(elapsed.TotalMinutes)} min ago";
        }

        if (elapsed.TotalDays < 1)
        {
            return $"{Math.Floor(elapsed.TotalHours)} h ago";
        }

        return $"{Math.Floor(elapsed.TotalDays)} d ago";
    }
}
