using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using ProjectManager.Core.Enums;

namespace ProjectManager.Core.Entities;

public class ApplicationUser : IdentityUser
{
    [Required]
    [StringLength(150)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    public UserRole Role { get; set; }

    public ICollection<Project> ClientProjects { get; set; } = new List<Project>();

    public ICollection<Project> SeniorDevProjects { get; set; } = new List<Project>();

    public ICollection<ProjectTask> AssignedTasks { get; set; } = new List<ProjectTask>();

    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}
