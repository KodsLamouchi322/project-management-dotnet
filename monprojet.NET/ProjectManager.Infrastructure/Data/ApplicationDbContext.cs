using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ProjectManager.Core.Entities;

namespace ProjectManager.Infrastructure.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    // Les DbSet représentent les futures tables dans la base de données SQL.
    // Entity Framework (EF Core) va traduire chaque ligne ici en une vraie table (ex: table "Projects").
    public DbSet<Project> Projects => Set<Project>();

    public DbSet<ProjectTask> ProjectTasks => Set<ProjectTask>();

    public DbSet<Notification> Notifications => Set<Notification>();

    public DbSet<DailySnapshot> DailySnapshots => Set<DailySnapshot>();

    public DbSet<ProjectNote> ProjectNotes => Set<ProjectNote>();

    // "Fluent API" : Cette méthode configure finement les règles de la base de données (clés étrangères, comportement de suppression).
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Project>()
            .HasOne(p => p.Client)
            .WithMany(u => u.ClientProjects)
            .HasForeignKey(p => p.ClientId)
            // Restrict : Si on essaie de supprimer le Client, la base de données refusera 
            // pour ne pas rendre le projet "orphelin". Ça protège l'intégrité des données.
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Project>()
            .HasOne(p => p.SeniorDev)
            .WithMany(u => u.SeniorDevProjects)
            .HasForeignKey(p => p.SeniorDevId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<ProjectTask>()
            .HasOne(pt => pt.Project)
            .WithMany(p => p.Tasks)
            .HasForeignKey(pt => pt.ProjectId)
            // Cascade : À l'inverse, si on supprime un Projet, 
            // TOUTES ses tâches sont automatiquement détruites avec lui. C'est le comportement "Cascade".
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ProjectTask>()
            .HasOne(pt => pt.Developer)
            .WithMany(u => u.AssignedTasks)
            .HasForeignKey(pt => pt.DeveloperId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Notification>()
            .HasOne(n => n.User)
            .WithMany(u => u.Notifications)
            .HasForeignKey(n => n.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<DailySnapshot>()
            .HasOne(snapshot => snapshot.Project)
            .WithMany(project => project.DailySnapshots)
            .HasForeignKey(snapshot => snapshot.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ProjectNote>()
            .HasOne(note => note.Project)
            .WithMany(project => project.Notes)
            .HasForeignKey(note => note.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ProjectNote>()
            .HasOne(note => note.Author)
            .WithMany()
            .HasForeignKey(note => note.AuthorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<DailySnapshot>()
            // Création d'un "Index Unique Composé" sur deux colonnes.
            // Ça garantit au niveau de SQL qu'on ne peut jamais avoir deux Snapshots pour le MÊME projet le MÊME jour.
            .HasIndex(snapshot => new { snapshot.ProjectId, snapshot.Date })
            .IsUnique();
    }
}
