using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace monprojet.NET.Middleware;

public class GlobalExceptionHandlerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlerMiddleware> _logger;

    public GlobalExceptionHandlerMiddleware(RequestDelegate next, ILogger<GlobalExceptionHandlerMiddleware> logger)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(logger);

        _next = next;
        _logger = logger;
    }

    /// <summary>
    /// Middleware personnalisé. Un "Middleware" est un filtre par lequel passe chaque requête HTTP.
    /// Celui-ci agit comme un filet de sécurité. Si le code "plante" (Exception non gérée), il l'attrape,
    /// log l'erreur proprement, et redirige l'utilisateur vers une belle page d'erreur (/Error) au lieu d'afficher du code source.
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);

            if (context.Response.HasStarted)
            {
                throw;
            }

            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.Redirect("/Error");
        }
    }
}
