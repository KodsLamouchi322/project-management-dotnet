using Microsoft.EntityFrameworkCore;
using ProjectManager.Core.Entities;
using ProjectManager.Infrastructure.Data;
using ProjectTaskStatus = ProjectManager.Core.Enums.TaskStatus;

namespace ProjectManager.Infrastructure.Services;

public class TaskService
{
    private readonly ApplicationDbContext _dbContext;

    public TaskService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Returns tasks for the given project.
    /// </summary>
    public async Task<IReadOnlyList<ProjectTask>> GetTasksByProject(int projectId)
    {
        var tasks = await _dbContext.ProjectTasks
            .Include(t => t.Developer)
            .Where(t => t.ProjectId == projectId)
            .OrderByDescending(t => t.AssignedDate)
            .ToListAsync();

        return tasks;
    }

    /// <summary>
    /// Returns tasks assigned to a developer during a specific month and year.
    /// </summary>
    public async Task<IReadOnlyList<ProjectTask>> GetTasksByDeveloper(string developerId, int month, int year)
    {
        if (string.IsNullOrWhiteSpace(developerId))
        {
            throw new ArgumentException("Developer id is required.", nameof(developerId));
        }

        if (month is < 1 or > 12)
        {
            throw new ArgumentOutOfRangeException(nameof(month));
        }

        if (year < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(year));
        }

        var tasks = await _dbContext.ProjectTasks
            .Include(t => t.Project)
            .Where(t => t.DeveloperId == developerId
                        && t.AssignedDate.Month == month
                        && t.AssignedDate.Year == year)
            .OrderByDescending(t => t.AssignedDate)
            .ToListAsync();

        return tasks;
    }

    /// <summary>
    /// Calcule le score d'un développeur pour un mois précis.
    /// Ce score est un ratio (pourcentage) d'efficacité : Nombre de tâches complétées divisé par le nombre total de tâches assignées ce mois-là.
    /// </summary>
    public async Task<double> CalculateDeveloperScore(string developerId, int month, int year)
    {
        if (string.IsNullOrWhiteSpace(developerId))
        {
            throw new ArgumentException("Developer id is required.", nameof(developerId));
        }

        if (month is < 1 or > 12)
        {
            throw new ArgumentOutOfRangeException(nameof(month));
        }

        if (year < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(year));
        }

        var assignedTasks = await _dbContext.ProjectTasks
            .Where(t => t.DeveloperId == developerId
                        && t.AssignedDate.Month == month
                        && t.AssignedDate.Year == year)
            .ToListAsync();

        // Si aucune tâche n'est assignée ce mois-ci, le score est logiquement de 0 pour éviter une division par zéro plus bas.
        if (assignedTasks.Count == 0)
        {
            return 0d;
        }

        var completedTasks = assignedTasks.Count(t => t.Status == ProjectTaskStatus.Completed);
        return (double)completedTasks / assignedTasks.Count;
    }

    /// <summary>
    /// Même logique que pour le mois, mais appliqué à la semaine en cours.
    /// C'est utilisé pour le "Leaderboard" (Top Developers) dans le tableau de bord SeniorDev.
    /// </summary>
    public async Task<double> CalculateDeveloperWeeklyScore(string developerId, DateTime referenceDate)
    {
        if (string.IsNullOrWhiteSpace(developerId))
        {
            throw new ArgumentException("Developer id is required.", nameof(developerId));
        }

        // Logique mathématique pour trouver le "Lundi" de la semaine actuelle en reculant jour par jour.
        var weekStart = referenceDate.Date;
        while (weekStart.DayOfWeek != DayOfWeek.Monday)
        {
            weekStart = weekStart.AddDays(-1);
        }

        // La fin de la semaine est simplement le lundi + 7 jours.
        var weekEnd = weekStart.AddDays(7);

        // [OPTIMISATION] : Au lieu de récupérer toutes les tâches avec ToListAsync(),
        // on utilise CountAsync() qui exécute un simple "SELECT COUNT(*)" dans SQL Server, ce qui est extrêmement rapide.
        var assignedTasksCount = await _dbContext.ProjectTasks
            .CountAsync(task => task.DeveloperId == developerId
                                && task.AssignedDate >= weekStart
                                && task.AssignedDate < weekEnd);

        if (assignedTasksCount == 0)
        {
            return 0d;
        }

        var completedTasksCount = await _dbContext.ProjectTasks
            .CountAsync(task => task.DeveloperId == developerId
                                && task.AssignedDate >= weekStart
                                && task.AssignedDate < weekEnd
                                && task.Status == ProjectTaskStatus.Completed);

        return (double)completedTasksCount / assignedTasksCount;
    }
}
