using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ProjectManager.Core.Entities;
using ProjectManager.Core.Enums;
using ProjectManager.Infrastructure.Data;
using ProjectTaskStatus = ProjectManager.Core.Enums.TaskStatus;

namespace ProjectManager.Infrastructure.Services;

public class DailySnapshotBackgroundService : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(1);

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DailySnapshotBackgroundService> _logger;

    public DailySnapshotBackgroundService(IServiceProvider serviceProvider, ILogger<DailySnapshotBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    // ExecuteAsync est la méthode d'entrée appelée par ASP.NET au démarrage de l'app.
    // Elle tourne en boucle "infinie" en arrière-plan tant que l'application est vivante.

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DailySnapshotBackgroundService started.");

        using var timer = new PeriodicTimer(CheckInterval);

        await CaptureDailySnapshotsAsync(stoppingToken);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await CaptureDailySnapshotsAsync(stoppingToken);
        }
    }

    private async Task CaptureDailySnapshotsAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Parce que c'est un BackgroundService, il n'y a pas de "requête web HTTP" attachée.
            // On doit donc "Créer un Scope" manuellement pour récupérer la base de données (DbContext).
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var today = DateTime.Today;

            var projects = await dbContext.Projects
                .Include(project => project.Tasks)
                .Where(project => !project.IsDeleted && project.Status == ProjectStatus.InProgress)
                .ToListAsync(cancellationToken);

            foreach (var project in projects)
            {
                // On vérifie si on n'a pas DÉJÀ enregistré la snapshot d'aujourd'hui pour ce projet.
                // Cela empêche d'avoir des données en double si le service tourne toutes les heures.
                var exists = await dbContext.DailySnapshots.AnyAsync(
                    snapshot => snapshot.ProjectId == project.Id && snapshot.Date == today,
                    cancellationToken);

                if (exists)
                {
                    continue; // Passe au projet suivant
                }

                // Calcul de la progression (règle de trois classique)
                var totalTasks = project.Tasks.Count;
                var completedTasks = project.Tasks.Count(task => task.Status == ProjectTaskStatus.Completed);
                var progress = totalTasks == 0 ? 0d : (double)completedTasks / totalTasks * 100d;

                // Insertion de "la photo à l'instant T" dans la base. C'est ce qui permet de tracer les graphiques d'évolution !
                dbContext.DailySnapshots.Add(new DailySnapshot
                {
                    ProjectId = project.Id,
                    Date = today,
                    ProgressPercent = progress
                });
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Daily snapshots captured for {Count} projects.", projects.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error capturing daily snapshots.");
        }
    }
}
