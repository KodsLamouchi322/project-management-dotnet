using Microsoft.AspNetCore.Mvc;

namespace monprojet.NET.Controllers;

public class ErrorController : Controller
{
    /// <summary>
    /// Displays the friendly generic error page.
    /// </summary>
    [HttpGet("/Error")]
    public IActionResult Index()
    {
        Response.StatusCode = StatusCodes.Status500InternalServerError;
        return View();
    }

    /// <summary>
    /// Displays custom status code pages.
    /// </summary>
    [HttpGet("/Error/{statusCode:int}")]
    public IActionResult StatusCodePage(int statusCode)
    {
        Response.StatusCode = statusCode;

        return statusCode switch
        {
            StatusCodes.Status403Forbidden => View("Forbidden"),
            StatusCodes.Status404NotFound => View("NotFound"),
            _ => View("Index")
        };
    }
}
