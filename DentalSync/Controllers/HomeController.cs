using DentalSync.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace DentalSync.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        // =========================================================================
        // HOME NAVIGATION ROUTES ONLY
        // =========================================================================

        public IActionResult Dashboard()
        {
            return RedirectToAction("Dashboard", "Dashboard");
        }

        public IActionResult Users()
        {
            return RedirectToAction("Users", "Users");
        }

        public IActionResult Services(string search = "", string status = "", int page = 1)
        {
            return RedirectToAction("Index", "Services", new { search, status, page });
        }

        public IActionResult Audit(string search = "", string role = "", int page = 1)
        {
            return RedirectToAction("Audit", "Audit", new { search, role, page });
        }

        public IActionResult Authentication(string search = "", string role = "", int page = 1)
        {
            return RedirectToAction("Authentication", "Audit", new { search, role, page });
        }

        public IActionResult Patients(string search = "", int page = 1)
        {
            return RedirectToAction("Register_Patients", "Receptionist", new { search, page });
        }

        public IActionResult Records(string preset = "month", DateTime? from = null, DateTime? to = null, int invoicePage = 1)
        {
            return RedirectToAction("Records", "Reports", new { preset, from, to, invoicePage });
        }

        // =========================================================================
        // SYSTEM & UTILITY HANDLERS
        // =========================================================================

        [AllowAnonymous]
        public IActionResult Promotionpage()
        {
            return View("~/Views/LandingPage/Promotionpage.cshtml");
        }

        [AllowAnonymous]
        public IActionResult Privacy()
        {
            return View();
        }

        [AllowAnonymous]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
