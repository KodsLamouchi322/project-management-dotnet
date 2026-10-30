using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using monprojet.NET.Models.Project;
using ProjectManager.Core.Entities;
using ProjectManager.Core.Enums;
using ProjectManager.Infrastructure.Data;
using ProjectManager.Infrastructure.Services;
using ProjectTaskStatus = ProjectManager.Core.Enums.TaskStatus;
using System.Security.Claims;

namespace monprojet.NET.Controllers;

public class ProjectController : Controller
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ProjectService _projectService;
    private readonly TaskService _taskService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly INotificationService _notificationService;

    public ProjectController(
        ApplicationDbContext dbContext,
        ProjectService projectService,
        TaskService taskService,
        UserManager<ApplicationUser> userManager,
        INotificationService notificationService)
    {
        _dbContext = dbContext;
        _projectService = projectService;
        _taskService = taskService;
        _userManager = userManager;
        _notificationService = notificationService;
    }

    [HttpGet]
    [Authorize(Roles = nameof(UserRole.SeniorDev))]
    public async Task<IActionResult> Index()
    {
        var progressByProject = await _projectService.UpdateProjectStatus();

        var projects = await _dbContext.Projects
            .Include(p => p.Tasks)
            .Where(p => !p.IsDeleted)
            .OrderByDescending(p => p.StartDate)
            .ToListAsync();

        var model = projects.Select(p => new ProjectListItemViewModel
        {
            Id = p.Id,
            Name = p.Name,
            Status = p.Status,
            TaskCount = p.Tasks.Count,
            ProgressPercentage = progressByProject.TryGetValue(p.Id, out var value) ? value : 0d
        });

        return View(model);
    }

    /// <summary>
    /// Displays the list of projects that have messaging enabled for the current user.
    /// </summary>
    [HttpGet]
    [Authorize(Roles = nameof(UserRole.SeniorDev) + "," + nameof(UserRole.Client) + "," + nameof(UserRole.Developer))]
    public async Task<IActionResult> Messages()
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(currentUserId))
        {
            return Forbid();
        }

        var projectsQuery = _dbContext.Projects
            .Where(project => !project.IsDeleted);

        if (User.IsInRole(nameof(UserRole.Client)))
        {
            projectsQuery = projectsQuery.Where(project => project.ClientId == currentUserId);
        }
        else if (User.IsInRole(nameof(UserRole.Developer)))
        {
            projectsQuery = projectsQuery.Where(project => project.Tasks.Any(task => task.DeveloperId == currentUserId));
        }
        else if (User.IsInRole(nameof(UserRole.SeniorDev)))
        {
            projectsQuery = projectsQuery.Where(project => project.SeniorDevId == currentUserId);
        }

        var projects = await projectsQuery
            .Select(project => new
            {
                project.Id,
                project.Name,
                project.Status,
                LastMessageAt = project.Notes
                    .OrderByDescending(note => note.CreatedAt)
                    .Select(note => (DateTime?)note.CreatedAt)
                    .FirstOrDefault(),
                LastMessage = project.Notes
                    .OrderByDescending(note => note.CreatedAt)
                    .Select(note => note.Message)
                    .FirstOrDefault()
            })
            .OrderByDescending(project => project.LastMessageAt ?? DateTime.MinValue)
            .ToListAsync();

        var model = projects
            .Select(project => new ProjectMessageListItemViewModel(
                project.Id,
                project.Name,
                project.Status,
                project.LastMessageAt,
                project.LastMessage))
            .ToList();

        return View(model);
    }

    [HttpGet]
    [Authorize(Roles = nameof(UserRole.SeniorDev) + "," + nameof(UserRole.Client) + "," + nameof(UserRole.Developer))]
    public async Task<IActionResult> Details(int id)
    {
        var project = await _dbContext.Projects
            .Include(p => p.Client)
            .Include(p => p.SeniorDev)
            .Include(p => p.Tasks)
                .ThenInclude(t => t.Developer)
            .Include(p => p.Notes)
                .ThenInclude(n => n.Author)
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);

        if (project is null)
        {
            return NotFound();
        }

        if (User.IsInRole(nameof(UserRole.Client)))
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.Equals(project.ClientId, currentUserId, StringComparison.Ordinal))
            {
                return Forbid();
            }
        }

        if (User.IsInRole(nameof(UserRole.Developer)))
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var assignedToProject = project.Tasks.Any(task => string.Equals(task.DeveloperId, currentUserId, StringComparison.Ordinal));
            if (!assignedToProject)
            {
                return Forbid();
            }
        }

        var totalTasks = project.Tasks.Count;
        var completedTasks = project.Tasks.Count(t => t.Status == ProjectTaskStatus.Completed);
        var progress = totalTasks == 0 ? 0d : (double)completedTasks / totalTasks * 100d;
        var daysRemaining = (project.Deadline.Date - DateTime.Now.Date).Days;

        var isClient = User.IsInRole(nameof(UserRole.Client));

        var tasks = project.Tasks
            .OrderBy(task => task.Deadline)
            .Select(task => new ProjectTaskDetailItemViewModel(
                task.Id,
                task.Title,
                isClient ? "-" : task.Developer?.FullName ?? "Unassigned",
                task.Priority,
                task.Deadline,
                task.Status,
                task.ClientRating))
            .ToList();

        var notes = project.Notes
            .OrderBy(note => note.CreatedAt)
            .Select(note => new ProjectNoteViewModel(
                note.Author?.FullName ?? "-",
                note.Message,
                note.CreatedAt))
            .ToList();

        var model = new ProjectDetailViewModel(
            project.Id,
            project.Name,
            project.Description,
            project.Client?.FullName ?? "-",
            project.SeniorDev?.FullName ?? "-",
            project.StartDate,
            project.EndDate,
            project.Deadline,
            project.Status,
            Math.Round(progress, 2),
            completedTasks,
            totalTasks,
            daysRemaining,
            project.ClientRating,
            User.IsInRole(nameof(UserRole.Client)),
            notes,
            tasks);

        return View(model);
    }

    /// <summary>
    /// Saves a project rating submitted by the owning client.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = nameof(UserRole.Client))]
    public async Task<IActionResult> Rate(int id, int rating)
    {
        if (rating is < 1 or > 5)
        {
            return BadRequest();
        }

        var project = await _dbContext.Projects
            .FirstOrDefaultAsync(item => item.Id == id && !item.IsDeleted);
        if (project is null)
        {
            return NotFound();
        }

        var clientId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(clientId))
        {
            return Forbid();
        }

        if (!string.Equals(project.ClientId, clientId, StringComparison.Ordinal))
        {
            return Forbid();
        }

        project.ClientRating = rating;
        await _dbContext.SaveChangesAsync();

        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// Adds a project message between the client and developers assigned to the project.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = nameof(UserRole.Client) + "," + nameof(UserRole.Developer) + "," + nameof(UserRole.SeniorDev))]
    public async Task<IActionResult> AddNote(int id, string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            TempData["NoteError"] = "Message is required.";
            return RedirectToAction(nameof(Details), new { id });
        }

        if (message.Length > 2000)
        {
            TempData["NoteError"] = "Message is too long.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var project = await _dbContext.Projects
            .Include(p => p.Tasks)
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);

        if (project is null)
        {
            return NotFound();
        }

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(currentUserId))
        {
            return Forbid();
        }

        if (User.IsInRole(nameof(UserRole.Client)) && !string.Equals(project.ClientId, currentUserId, StringComparison.Ordinal))
        {
            return Forbid();
        }

        if (User.IsInRole(nameof(UserRole.Developer))
            && !project.Tasks.Any(task => string.Equals(task.DeveloperId, currentUserId, StringComparison.Ordinal)))
        {
            return Forbid();
        }

        if (User.IsInRole(nameof(UserRole.SeniorDev))
            && !string.Equals(project.SeniorDevId, currentUserId, StringComparison.Ordinal))
        {
            return Forbid();
        }

        var note = new ProjectNote
        {
            ProjectId = project.Id,
            AuthorId = currentUserId,
            Message = message.Trim(),
            CreatedAt = DateTime.Now
        };

        _dbContext.ProjectNotes.Add(note);
        await _dbContext.SaveChangesAsync();

        var recipientIds = new HashSet<string>(StringComparer.Ordinal);

        if (!string.IsNullOrWhiteSpace(project.ClientId))
        {
            recipientIds.Add(project.ClientId);
        }

        if (!string.IsNullOrWhiteSpace(project.SeniorDevId))
        {
            recipientIds.Add(project.SeniorDevId);
        }

        foreach (var developerId in project.Tasks
                     .Select(task => task.DeveloperId)
                     .Where(id => !string.IsNullOrWhiteSpace(id)))
        {
            recipientIds.Add(developerId!);
        }

        recipientIds.Remove(currentUserId);

        foreach (var recipientId in recipientIds)
        {
            await _notificationService.CreateAsync(
                recipientId,
                $"New project message on {project.Name}.");
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpGet]
    [Authorize(Roles = nameof(UserRole.SeniorDev))]
    public async Task<IActionResult> Create()
    {
        var model = new ProjectFormViewModel
        {
            StartDate = DateTime.Today,
            Deadline = DateTime.Today,
            Clients = await GetClientsAsync(),
            Developers = await GetDevelopersAsync()
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = nameof(UserRole.SeniorDev))]
    public async Task<IActionResult> Create(ProjectFormViewModel model, List<CreateTaskViewModel>? Tasks)
    {
        Tasks ??= new List<CreateTaskViewModel>();
        var today = DateTime.Today;

        if (Tasks.Count == 0)
        {
            ModelState.AddModelError(nameof(ProjectFormViewModel.PendingTasks), "At least one task is required.");
        }

        for (var i = 0; i < Tasks.Count; i++)
        {
            var prefix = $"Tasks[{i}]";
            TryValidateModel(Tasks[i], prefix);
        }

        TryValidateModel(model);

        for (var i = 0; i < Tasks.Count; i++)
        {
            var task = Tasks[i];
            var prefix = $"Tasks[{i}].Deadline";

            if (task.Deadline.Date < today)
            {
                ModelState.AddModelError(prefix, "Task deadline cannot be in the past.");
            }

            if (task.Deadline.Date <= model.StartDate.Date)
            {
                ModelState.AddModelError(prefix, "Task deadline must be after the project start date (at least 1 day after).");
            }

            if (task.Deadline.Date > model.Deadline.Date)
            {
                ModelState.AddModelError(prefix, "Task deadline cannot be after the project deadline.");
            }
        }

        if (!ModelState.IsValid)
        {
            model.PendingTasks = Tasks;
            model.Clients = await GetClientsAsync();
            model.Developers = await GetDevelopersAsync();
            return View(model);
        }

        var seniorDevId = _userManager.GetUserId(User);
        if (string.IsNullOrEmpty(seniorDevId))
        {
            return Forbid();
        }

        var project = new Project
        {
            Name = model.Name,
            Description = model.Description,
            StartDate = model.StartDate,
            EndDate = null,
            Deadline = model.Deadline,
            ClientId = model.ClientId,
            SeniorDevId = seniorDevId,
            Status = ProjectStatus.InProgress,
            IsDeleted = false
        };

        _dbContext.Projects.Add(project);
        await _dbContext.SaveChangesAsync();

        var taskEntities = Tasks.Select(task => new ProjectTask
        {
            ProjectId = project.Id,
            Title = task.Title,
            Description = task.Description,
            Priority = task.Priority,
            Deadline = task.Deadline,
            Duration = task.Duration,
            DeveloperId = task.DeveloperId,
            AssignedDate = DateTime.Now,
            Status = ProjectTaskStatus.ToDo
        }).ToList();

        _dbContext.ProjectTasks.AddRange(taskEntities);
        await _dbContext.SaveChangesAsync();

        foreach (var task in taskEntities)
        {
            await _notificationService.CreateAsync(
                task.DeveloperId,
                $"New task assigned: {task.Title} on project {project.Name}",
                task.Id);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    [Authorize(Roles = nameof(UserRole.SeniorDev))]
    public async Task<IActionResult> Edit(int id)
    {
        var project = await _dbContext.Projects
            .Include(item => item.Tasks)
                .ThenInclude(item => item.Developer)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);

        if (project is null)
        {
            return NotFound();
        }

        var seniorDevId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(seniorDevId))
        {
            return Forbid();
        }

        if (!string.Equals(project.SeniorDevId, seniorDevId, StringComparison.Ordinal))
        {
            return Forbid();
        }

        var model = new ProjectFormViewModel
        {
            Id = project.Id,
            Name = project.Name,
            Description = project.Description,
            StartDate = project.StartDate,
            Deadline = project.Deadline,
            ClientId = project.ClientId,
            Clients = await GetClientsAsync(),
            Tasks = project.Tasks
                .OrderBy(item => item.Deadline)
                .Select(item => new ProjectTaskEditListItemViewModel(
                    item.Id,
                    item.Title,
                    item.Developer?.FullName ?? "Unassigned",
                    item.Status.ToString(),
                    item.Deadline))
                .ToList()
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = nameof(UserRole.SeniorDev))]
    public async Task<IActionResult> Edit(ProjectFormViewModel model)
    {
        if (model.Id is null)
        {
            return BadRequest();
        }

        var project = await _dbContext.Projects
            .Include(p => p.Tasks)
            .FirstOrDefaultAsync(p => p.Id == model.Id.Value && !p.IsDeleted);

        if (project is null)
        {
            return NotFound();
        }

        var seniorDevId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(seniorDevId))
        {
            return Forbid();
        }

        if (!string.Equals(project.SeniorDevId, seniorDevId, StringComparison.Ordinal))
        {
            return Forbid();
        }

        TryValidateModel(model);

        if (!project.Tasks.Any())
        {
            ModelState.AddModelError(
                nameof(ProjectFormViewModel.Tasks),
                "A project must contain at least one task.");
        }

        foreach (var task in project.Tasks)
        {
            if (task.Deadline.Date > model.Deadline.Date)
            {
                ModelState.AddModelError(
                    nameof(ProjectFormViewModel.Deadline),
                    $"The task \"{task.Title}\" has deadline {task.Deadline:dd/MM/yyyy}, which is after the project deadline. Change task deadlines first.");
            }

            if (task.Deadline.Date <= model.StartDate.Date)
            {
                ModelState.AddModelError(
                    nameof(ProjectFormViewModel.StartDate),
                    $"The task \"{task.Title}\" has deadline {task.Deadline:dd/MM/yyyy}, which is on or before the new start date. Task deadlines must be at least 1 day after the start date. Adjust task deadlines first.");
            }
        }

        if (!ModelState.IsValid)
        {
            model.Clients = await GetClientsAsync();
            model.Tasks = project.Tasks
                .OrderBy(item => item.Deadline)
                .Select(item => new ProjectTaskEditListItemViewModel(
                    item.Id,
                    item.Title,
                    item.Developer?.FullName ?? "Unassigned",
                    item.Status.ToString(),
                    item.Deadline))
                .ToList();
            return View(model);
        }

        project.Name = model.Name;
        project.Description = model.Description;
        project.StartDate = model.StartDate;
        project.Deadline = model.Deadline;
        project.ClientId = model.ClientId;

        await _dbContext.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Displays team members and their project tasks for a specific project.
    /// </summary>
    [HttpGet("/Project/{id:int}/Team")]
    [Authorize(Roles = nameof(UserRole.SeniorDev) + "," + nameof(UserRole.Developer) + "," + nameof(UserRole.Client))]
    public async Task<IActionResult> Team(int id)
    {
        var project = await _dbContext.Projects
            .Include(p => p.Client)
            .Include(p => p.SeniorDev)
            .Include(p => p.Tasks)
                .ThenInclude(t => t.Developer)
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);

        if (project is null)
        {
            return NotFound();
        }

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(currentUserId))
        {
            return Forbid();
        }

        var isOwnerSeniorDev = User.IsInRole(nameof(UserRole.SeniorDev))
                               && string.Equals(project.SeniorDevId, currentUserId, StringComparison.Ordinal);
        var isProjectDeveloper = User.IsInRole(nameof(UserRole.Developer))
                                 && project.Tasks.Any(task => string.Equals(task.DeveloperId, currentUserId, StringComparison.Ordinal));
        var isProjectClient = User.IsInRole(nameof(UserRole.Client))
                              && string.Equals(project.ClientId, currentUserId, StringComparison.Ordinal);

        if (!isOwnerSeniorDev && !isProjectDeveloper && !isProjectClient)
        {
            return Forbid();
        }

        var developerGroups = project.Tasks
            .Where(task => task.Developer is not null)
            .GroupBy(task => task.Developer!)
            .OrderBy(group => group.Key.FullName)
            .ToList();

        var developers = new List<ProjectTeamDeveloperViewModel>();

        foreach (var group in developerGroups)
        {
            var developer = group.Key;
            var score = await _taskService.CalculateDeveloperWeeklyScore(developer.Id, DateTime.Now);

            var tasks = group
                .OrderBy(task => task.Deadline)
                .Select(task => new ProjectTeamTaskItemViewModel(
                    task.Title,
                    task.Priority,
                    task.Status,
                    task.Deadline))
                .ToList();

            developers.Add(new ProjectTeamDeveloperViewModel(
                developer.Id,
                developer.FullName,
                GetInitials(developer.FullName),
                Math.Round(score * 100d, 2),
                tasks));
        }

        var model = new ProjectTeamViewModel(
            project.Id,
            project.Name,
            new TeamRoleCardViewModel(
                project.SeniorDev?.FullName ?? "-",
                nameof(UserRole.SeniorDev),
                GetInitials(project.SeniorDev?.FullName ?? "-")),
            new TeamRoleCardViewModel(
                project.Client?.FullName ?? "-",
                nameof(UserRole.Client),
                GetInitials(project.Client?.FullName ?? "-")),
            developers);

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = nameof(UserRole.SeniorDev))]
    public async Task<IActionResult> Delete(int id)
    {
        var project = await _dbContext.Projects
            .Include(p => p.Tasks)
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);

        if (project is null)
        {
            return NotFound();
        }

        if (project.Status is ProjectStatus.InProgress or ProjectStatus.Late)
        {
            TempData["Error"] = "Cannot delete a project that is InProgress or Late.";
            return RedirectToAction(nameof(Index));
        }

        if (project.Tasks.Any())
        {
            TempData["Error"] = "Cannot delete a project with assigned tasks.";
            return RedirectToAction(nameof(Index));
        }

        project.IsDeleted = true;
        await _dbContext.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    private async Task<IEnumerable<SelectListItem>> GetClientsAsync()
    {
        var clientRoleUsers = await _userManager.GetUsersInRoleAsync(nameof(UserRole.Client));

        return clientRoleUsers
            .OrderBy(u => u.FullName)
            .Select(u => new SelectListItem
            {
                Value = u.Id,
                Text = $"{u.FullName} ({u.Email})"
            })
            .ToList();
    }

    private async Task<IEnumerable<SelectListItem>> GetDevelopersAsync()
    {
        var developers = await _userManager.GetUsersInRoleAsync(nameof(UserRole.Developer));

        return developers
            .OrderBy(u => u.FullName)
            .Select(u => new SelectListItem
            {
                Value = u.Id,
                Text = $"{u.FullName} ({u.Email})"
            })
            .ToList();
    }

    private static string GetInitials(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return "?";
        }

        return string.Concat(fullName
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part[0]))
            .ToUpperInvariant();
    }
}
