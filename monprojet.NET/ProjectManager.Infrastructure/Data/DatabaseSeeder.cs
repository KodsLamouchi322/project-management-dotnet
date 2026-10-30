using Microsoft.AspNetCore.Identity;
using ProjectManager.Core.Entities;
using ProjectManager.Core.Enums;

namespace ProjectManager.Infrastructure.Data;

public static class DatabaseSeeder
{
    // Le "Seeder" sert à initialiser la base de données avec des données par défaut.
    // Au premier lancement du projet (voir Program.cs), si la base est vide, ce fichier crée
    // automatiquement les rôles (Admin, Client...) et le tout premier compte Administrateur.
    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);

        using var scope = serviceProvider.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var roles = new[]
        {
            UserRole.Client.ToString(),
            UserRole.Developer.ToString(),
            UserRole.SeniorDev.ToString(),
            UserRole.Admin.ToString()
        };

        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                var roleResult = await roleManager.CreateAsync(new IdentityRole(role));
                if (!roleResult.Succeeded)
                {
                    throw new InvalidOperationException($"Role creation failed for '{role}'.");
                }
            }
        }

        const string adminEmail = "admin@pm.com";
        const string adminPassword = "Admin@123";

        var adminUser = await userManager.FindByEmailAsync(adminEmail);
        if (adminUser is null)
        {
            adminUser = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                FullName = "System Administrator",
                Role = UserRole.Admin,
                EmailConfirmed = true
            };

            var userResult = await userManager.CreateAsync(adminUser, adminPassword);
            if (!userResult.Succeeded)
            {
                throw new InvalidOperationException("Default admin user creation failed.");
            }
        }

        if (!await userManager.IsInRoleAsync(adminUser, UserRole.Admin.ToString()))
        {
            var addRoleResult = await userManager.AddToRoleAsync(adminUser, UserRole.Admin.ToString());
            if (!addRoleResult.Succeeded)
            {
                throw new InvalidOperationException("Assigning Admin role to default admin user failed.");
            }
        }
    }
}
