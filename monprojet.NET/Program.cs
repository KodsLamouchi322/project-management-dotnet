using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using monprojet.NET.Middleware;
using ProjectManager.Core.Entities;
using ProjectManager.Infrastructure.Data;
using ProjectManager.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// PHASE 1 : INJECTION DE DÉPENDANCES (SERVICES)
// On ajoute des "briques" (services) à notre application avant de la démarrer.
// Add services to the container.
builder.Services.AddControllersWithViews(options =>
{
    // Filtre de sécurité global : Par défaut, IL FAUT être connecté 
    // (RequireAuthenticatedUser) pour voir n'importe quelle page du site, sauf indication contraire.
    var authorizationPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();

    options.Filters.Add(new AuthorizeFilter(authorizationPolicy));
});
builder.Services.AddLocalization();
// On connecte Entity Framework Core à SQL Server en lisant la chaîne de connexion ("DefaultConnection") dans le appsettings.json.
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
// Configuration du système de comptes utilisateurs et rôles (Identity)
builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
});
builder.Services.Configure<CookiePolicyOptions>(options =>
{
    options.Secure = CookieSecurePolicy.Always;
    options.MinimumSameSitePolicy = SameSiteMode.Lax;
});
// Définition de la durée de vie des services (Lifetimes) :
// AddScoped : Le service est créé à chaque requête web (clic de l'utilisateur) puis détruit.
builder.Services.AddScoped<ProjectService>();
builder.Services.AddScoped<TaskService>();
builder.Services.AddScoped<INotificationService, NotificationService>();

// AddHostedService : Le service est créé UNE FOIS au démarrage et tourne en tâche de fond (boucle infinie).
builder.Services.AddHostedService<NotificationBackgroundService>();
builder.Services.AddHostedService<DailySnapshotBackgroundService>();

// L'application a fini de lire ses réglages, elle se "construit".
var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await dbContext.Database.MigrateAsync();
}

await DatabaseSeeder.SeedAsync(app.Services);

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

// PHASE 2 : LE PIPELINE HTTP (MIDDLEWARES)
// On définit l'ordre dans lequel chaque requête (clic) est traitée.
// Notre propre filet de sécurité anti-crash (500) est placé en PREMIER.
app.UseMiddleware<GlobalExceptionHandlerMiddleware>();
app.UseHttpsRedirection();
app.UseRequestLocalization();
app.UseRouting();

app.UseStatusCodePagesWithReExecute("/Error/{0}");

// 1. Est-ce que tu as un ticket valide ? (Authentication)
app.UseAuthentication();
// 2. Est-ce que tu as le droit de voir ça ? ex: Rôle Admin (Authorization)
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
