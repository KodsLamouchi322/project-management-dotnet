using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using monprojet.NET.Models.Admin;
using ProjectManager.Core.Entities;
using ProjectManager.Core.Enums;

namespace monprojet.NET.Controllers;

[Authorize(Roles = nameof(UserRole.Admin))]
public class AdminController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;

    public AdminController(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var users = _userManager.Users.ToList();
        var model = new List<AdminUserViewModel>(users.Count);

        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            var currentRole = roles.FirstOrDefault() ?? user.Role.ToString();

            model.Add(new AdminUserViewModel
            {
                UserId = user.Id,
                FullName = user.FullName,
                Email = user.Email ?? string.Empty,
                CurrentRole = currentRole,
                CanChangeRole = CanChangeRole(currentRole),
                AllowedNextRoles = GetAllowedNextRoles(currentRole)
            });
        }

        return View(model.OrderBy(u => u.FullName));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeRole(string userId, string newRole)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(newRole))
        {
            return BadRequest(new { success = false, message = "Invalid request." });
        }

        if (newRole.Equals(nameof(UserRole.Admin), StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { success = false, message = "Admin role cannot be assigned from UI." });
        }

        if (!Enum.TryParse<UserRole>(newRole, true, out var parsedRole))
        {
            return BadRequest(new { success = false, message = "Unknown role." });
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return NotFound(new { success = false, message = "User not found." });
        }

        var currentRoles = await _userManager.GetRolesAsync(user);
        var currentRole = currentRoles.FirstOrDefault() ?? user.Role.ToString();

        if (!IsTransitionAllowed(currentRole, parsedRole.ToString()))
        {
            return BadRequest(new { success = false, message = "Role transition not allowed." });
        }

        var removeResult = await _userManager.RemoveFromRolesAsync(user, currentRoles);
        if (!removeResult.Succeeded)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { success = false, message = "Failed to remove current role." });
        }

        var addResult = await _userManager.AddToRoleAsync(user, parsedRole.ToString());
        if (!addResult.Succeeded)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { success = false, message = "Failed to assign new role." });
        }

        user.Role = parsedRole;
        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { success = false, message = "Role changed, but user profile update failed." });
        }

        return Json(new { success = true, message = "Role updated successfully.", newRole = parsedRole.ToString() });
    }

    private static bool CanChangeRole(string currentRole)
        => currentRole is nameof(UserRole.Client) or nameof(UserRole.Developer) or nameof(UserRole.SeniorDev);

    private static IReadOnlyList<string> GetAllowedNextRoles(string currentRole)
    {
        return currentRole switch
        {
            nameof(UserRole.Client) => new[] { nameof(UserRole.Developer), nameof(UserRole.SeniorDev) },
            nameof(UserRole.Developer) => new[] { nameof(UserRole.Developer), nameof(UserRole.SeniorDev) },
            nameof(UserRole.SeniorDev) => new[] { nameof(UserRole.Developer), nameof(UserRole.SeniorDev) },
            _ => Array.Empty<string>()
        };
    }

    private static bool IsTransitionAllowed(string currentRole, string newRole)
    {
        if (currentRole.Equals(nameof(UserRole.Admin), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return currentRole switch
        {
            nameof(UserRole.Client) => newRole is nameof(UserRole.Developer) or nameof(UserRole.SeniorDev),
            nameof(UserRole.Developer) => newRole is nameof(UserRole.Developer) or nameof(UserRole.SeniorDev),
            nameof(UserRole.SeniorDev) => newRole is nameof(UserRole.Developer) or nameof(UserRole.SeniorDev),
            _ => false
        };
    }
}
