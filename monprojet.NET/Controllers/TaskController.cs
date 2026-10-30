using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using monprojet.NET.Models.TaskManagement;
using ProjectManager.Core.Entities;
using ProjectManager.Core.Enums;
using ProjectManager.Infrastructure.Data;
using ProjectManager.Infrastructure.Services;
using ProjectTaskStatus = ProjectManager.Core.Enums.TaskStatus;

namespace monprojet.NET.Controllers;

[Authorize(Roles = nameof(UserRole.SeniorDev))]
public class TaskController : Controller
{
    private readonly ApplicationDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly INotificationService _notificationService;

    public TaskController(
        ApplicationDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        INotificationService notificationService)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _notificationService = notificationService;
    }

    /// <summary>
    /// Displays the form to add a task to a project.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> AddTask(int projectId)
    {
        var project = await _dbContext.Projects
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == projectId && !p.IsDeleted);

        if (project is null)
        {
            return NotFound();
        }

        var model = new TaskFormViewModel
        {
            ProjectId = projectId,
            Deadline = project.StartDate > DateTime.Today ? project.StartDate : DateTime.Today,
            ProjectStartDate = project.StartDate,
            ProjectDeadline = project.Deadline,
            Duration = TimeSpan.FromHours(1),
            Developers = await GetDevelopersAsync()
        };

        ViewData["ProjectName"] = project.Name;
        return View(model);
    }

    /// <summary>
    /// Creates a task and notification for the assigned developer.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddTask(TaskFormViewModel model)
    {
        var project = await _dbContext.Projects
            .FirstOrDefaultAsync(p => p.Id == model.ProjectId && !p.IsDeleted);

        if (project is null)
        {
            return NotFound();
        }

        model.ProjectStartDate = project.StartDate;
        model.ProjectDeadline = project.Deadline;

        if (model.Deadline.Date < DateTime.Today)
        {
            ModelState.AddModelError(nameof(model.Deadline), "Task deadline cannot be in the past.");
        }

        if (model.Deadline.Date <= model.ProjectStartDate.Date)
        {
            ModelState.AddModelError(nameof(model.Deadline), "Task deadline must be after the project start date (at least 1 day after).");
        }

        if (model.Deadline.Date > model.ProjectDeadline.Date)
        {
            ModelState.AddModelError(nameof(model.Deadline), "Task deadline cannot be after the project deadline.");
        }

        TryValidateModel(model);

        if (!ModelState.IsValid)
        {
            model.Developers = await GetDevelopersAsync();
            ViewData["ProjectName"] = project.Name;
            return View(model);
        }

        var task = new ProjectTask
        {
            ProjectId = model.ProjectId,
            Title = model.Title,
            Description = model.Description,
            Priority = model.Priority,
            Deadline = model.Deadline,
            Duration = model.Duration,
            DeveloperId = model.DeveloperId,
            AssignedDate = DateTime.Now,
            Status = ProjectTaskStatus.ToDo
        };

        _dbContext.ProjectTasks.Add(task);
        await _dbContext.SaveChangesAsync();

        await _notificationService.CreateAsync(
            model.DeveloperId,
            $"New task assigned: {task.Title} on project {project.Name}",
            task.Id);

        return RedirectToAction("Details", "Project", new { id = model.ProjectId });
    }

    /// <summary>
    /// Displays task edit form.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> EditTask(int id)
    {
        var task = await _dbContext.ProjectTasks
            .Include(t => t.Project)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (task is null || task.Project is null || task.Project.IsDeleted)
        {
            return NotFound();
        }

        var model = new TaskFormViewModel
        {
            Id = task.Id,
            ProjectId = task.ProjectId,
            Title = task.Title,
            Description = task.Description,
            Priority = task.Priority,
            Deadline = task.Deadline,
            ProjectStartDate = task.Project.StartDate,
            ProjectDeadline = task.Project.Deadline,
            Duration = task.Duration,
            DeveloperId = task.DeveloperId,
            Developers = await GetDevelopersAsync()
        };

        ViewData["ProjectName"] = task.Project.Name;
        return View(model);
    }

    /// <summary>
    /// Updates task details.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditTask(TaskFormViewModel model)
    {
        if (model.Id is null)
        {
            return BadRequest();
        }

        var task = await _dbContext.ProjectTasks
            .Include(t => t.Project)
            .FirstOrDefaultAsync(t => t.Id == model.Id.Value);

        if (task is null || task.Project is null || task.Project.IsDeleted)
        {
            return NotFound();
        }

        model.ProjectStartDate = task.Project.StartDate;
        model.ProjectDeadline = task.Project.Deadline;

        if (model.Deadline.Date < DateTime.Today && model.Deadline.Date != task.Deadline.Date)
        {
            ModelState.AddModelError(nameof(model.Deadline), "Task deadline cannot be in the past.");
        }

        if (model.Deadline.Date <= model.ProjectStartDate.Date)
        {
            ModelState.AddModelError(nameof(model.Deadline), "Task deadline must be after the project start date (at least 1 day after).");
        }

        if (model.Deadline.Date > model.ProjectDeadline.Date)
        {
            ModelState.AddModelError(nameof(model.Deadline), "Task deadline cannot be after the project deadline.");
        }

        TryValidateModel(model);

        if (!ModelState.IsValid)
        {
            model.Developers = await GetDevelopersAsync();
            ViewData["ProjectName"] = task.Project.Name;
            return View(model);
        }

        task.Title = model.Title;
        task.Description = model.Description;
        task.Priority = model.Priority;
        task.Deadline = model.Deadline;
        task.Duration = model.Duration;
        task.DeveloperId = model.DeveloperId;

        await _dbContext.SaveChangesAsync();

        return RedirectToAction("Details", "Project", new { id = task.ProjectId });
    }

    /// <summary>
    /// Marks a task as completed.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CompleteTask(int id)
    {
        var task = await _dbContext.ProjectTasks.FirstOrDefaultAsync(t => t.Id == id);
        if (task is null)
        {
            return NotFound();
        }

        task.Status = ProjectTaskStatus.Completed;
        await _dbContext.SaveChangesAsync();

        return RedirectToAction("Details", "Project", new { id = task.ProjectId });
    }

    /// <summary>
    /// Deletes a task when allowed by status rules.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteTask(int id)
    {
        var task = await _dbContext.ProjectTasks.FirstOrDefaultAsync(t => t.Id == id);
        if (task is null)
        {
            return NotFound();
        }

        var taskCount = await _dbContext.ProjectTasks.CountAsync(t => t.ProjectId == task.ProjectId);
        if (taskCount <= 1)
        {
            TempData["TaskError"] = "Cannot delete the last task of a project. A project must keep at least one task.";
            return RedirectToAction("Details", "Project", new { id = task.ProjectId });
        }

        if (task.Status != ProjectTaskStatus.ToDo)
        {
            TempData["TaskError"] = "Cannot delete a task unless status is ToDo.";
            return RedirectToAction("Details", "Project", new { id = task.ProjectId });
        }

        _dbContext.ProjectTasks.Remove(task);
        await _dbContext.SaveChangesAsync();

        return RedirectToAction("Details", "Project", new { id = task.ProjectId });
    }

    private async Task<IEnumerable<SelectListItem>> GetDevelopersAsync()
    {
        var developers = await _userManager.GetUsersInRoleAsync(nameof(UserRole.Developer));

        return developers
            .OrderBy(d => d.FullName)
            .Select(d => new SelectListItem
            {
                Value = d.Id,
                Text = $"{d.FullName} ({d.Email})"
            })
            .ToList();
    }
}
