using Microsoft.EntityFrameworkCore;
using ProjectManager.Core.Entities;
using ProjectManager.Core.Enums;
using ProjectManager.Infrastructure.Data;
using ProjectTaskStatus = ProjectManager.Core.Enums.TaskStatus;
using Microsoft.Extensions.Localization;
using monprojet.NET.Resources;

namespace ProjectManager.Infrastructure.Services;

public class ProjectService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly INotificationService _notificationService;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public ProjectService(
        ApplicationDbContext dbContext,
        INotificationService notificationService,
        IStringLocalizer<SharedResource> localizer)
    {
        _dbContext = dbContext;
        _notificationService = notificationService;
        _localizer = localizer;
    }

    /// <summary>
    /// C'est le coeur du système de suivi. Cette méthode recalcule le statut et le % d'avancement de TOUS les projets.
    /// Elle est souvent appelée par les tableaux de bord pour garantir des données fraîches.
    /// </summary>
    public async Task<Dictionary<int, double>> UpdateProjectStatus(string? seniorDevId = null)
    {
        var now = DateTime.Now;
        var progressByProject = new Dictionary<int, double>();

        var projectsQuery = _dbContext.Projects
            .Include(p => p.Tasks)
            .Where(p => !p.IsDeleted);

        if (!string.IsNullOrWhiteSpace(seniorDevId))
        {
            projectsQuery = projectsQuery.Where(project => project.SeniorDevId == seniorDevId);
        }

        var projects = await projectsQuery.ToListAsync();

        foreach (var project in projects)
        {
            var previousStatus = project.Status;
            var totalTasks = project.Tasks.Count;
            var completedTasks = project.Tasks.Count(t => t.Status == ProjectTaskStatus.Completed);
            var progress = totalTasks == 0 ? 0d : (double)completedTasks / totalTasks * 100d;
            var roundedProgress = Math.Round(progress, 2);

            // Les règles de gestion automatiques :
            if (progress >= 100d && totalTasks > 0)
            {
                // Si tout est fini, le projet se ferme automatiquement aujourd'hui.
                project.Status = ProjectStatus.Completed;
                project.EndDate ??= DateTime.Today;
            }
            else if (now > project.Deadline)
            {
                // Si la date limite est passée, il passe en retard, à moins qu'il n'ait été fini au if précédent.
                project.Status = ProjectStatus.Late;
                project.EndDate = null;
            }
            else
            {
                project.Status = ProjectStatus.InProgress;
                project.EndDate = null;
            }

            progressByProject[project.Id] = roundedProgress;

            if (!string.IsNullOrWhiteSpace(project.ClientId))
            {
                // Mécanisme intelligent d'envoi de notifications par paliers.
                // On calcule le palier atteint (ex: si progress = 45%, le palier = 40)
                var progressMilestone = (int)Math.Floor(roundedProgress / 20d) * 20;
                
                // S'il a dépassé un palier qu'il n'avait pas encore atteint (LastProgressNotificationPercent)
                if (progressMilestone >= 20 && progressMilestone > project.LastProgressNotificationPercent)
                {
                    // Boucle au cas où il aurait sauté deux paliers d'un coup (ex: passage direct de 10% à 50%)
                    for (var milestone = project.LastProgressNotificationPercent + 20;
                         milestone <= progressMilestone;
                         milestone += 20)
                    {
                        var message = _localizer["ProjectProgressNotification", project.Name, milestone];
                        await _notificationService.CreateAsync(project.ClientId, message);
                    }

                    // On sauvegarde le nouveau record atteint.
                    project.LastProgressNotificationPercent = progressMilestone;
                }
            }

            if (previousStatus != project.Status && !string.IsNullOrWhiteSpace(project.ClientId))
            {
                var message = _localizer["ProjectStatusChangedNotification", project.Name, project.Status];
                await _notificationService.CreateAsync(project.ClientId, message);
            }
        }

        await _dbContext.SaveChangesAsync();
        return progressByProject;
    }
}
