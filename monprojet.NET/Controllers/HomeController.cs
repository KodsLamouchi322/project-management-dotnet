using Microsoft.AspNetCore.Mvc;
using monprojet.NET.Models;
using ProjectManager.Core.Enums;
using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;

namespace monprojet.NET.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            if (User.IsInRole(nameof(UserRole.Admin)))
            {
                return RedirectToAction(nameof(DashboardController.Admin), "Dashboard");
            }

            if (User.IsInRole(nameof(UserRole.SeniorDev)))
            {
                return RedirectToAction("SeniorDev", "Dashboard");
            }

            if (User.IsInRole(nameof(UserRole.Developer)))
            {
                return RedirectToAction("Developer", "Dashboard");
            }

            if (User.IsInRole(nameof(UserRole.Client)))
            {
                return RedirectToAction("Client", "Dashboard");
            }

            return RedirectToAction("Client", "Dashboard");
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [Authorize(Roles = nameof(UserRole.Client))]
        public IActionResult ClientDashboard()
        {
            return View();
        }

        [Authorize(Roles = nameof(UserRole.Developer))]
        public IActionResult DeveloperDashboard()
        {
            return View();
        }

        [Authorize(Roles = nameof(UserRole.SeniorDev))]
        public IActionResult SeniorDevDashboard()
        {
            return View();
        }

        [Authorize(Roles = nameof(UserRole.Admin))]
        public IActionResult AdminDashboard()
        {
            return RedirectToAction(nameof(DashboardController.Admin), "Dashboard");
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
