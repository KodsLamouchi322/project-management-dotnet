using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectManager.Core.Enums;
using ProjectManager.Infrastructure.Data;
using ProjectManager.Infrastructure.Services;
using System.Security.Claims;

namespace monprojet.NET.Controllers;

[Authorize(Roles = nameof(UserRole.Developer) + "," + nameof(UserRole.SeniorDev) + "," + nameof(UserRole.Client))]
public class NotificationsController : Controller
{
    private readonly INotificationService _notificationService;
    private readonly ApplicationDbContext _dbContext;

    public NotificationsController(INotificationService notificationService, ApplicationDbContext dbContext)
    {
        _notificationService = notificationService;
        _dbContext = dbContext;
    }

    /// <summary>
    /// Displays all notifications for the current user.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Forbid();
        }

        var notifications = await _dbContext.Notifications
            .Where(notification => notification.UserId == userId)
            .OrderByDescending(notification => notification.CreatedAt)
            .ToListAsync();

        return View(notifications);
    }

    /// <summary>
    /// Returns unread notification count for the current user.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> UnreadCount()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Forbid();
        }

        var unreadNotifications = await _notificationService.GetUnreadAsync(userId);
        return Json(new { unreadCount = unreadNotifications.Count });
    }

    /// <summary>
    /// Marks all notifications as read for the current user.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllAsRead()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Forbid();
        }

        await _notificationService.MarkAllAsReadAsync(userId);
        return Ok();
    }
}
