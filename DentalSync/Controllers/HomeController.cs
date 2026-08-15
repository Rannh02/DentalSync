using DentalSync.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;

namespace DentalSync.Controllers
{
    public class HomeController : Controller
    {

        //=================PATIENTS CARE=================
        public IActionResult Patients()
        {
            return View();
        }

        public IActionResult Dentists()
        {
            return View();
        }

        public IActionResult Appointments()
        {
            return View();
        }
        public IActionResult Records()
        {
            return View();
        }
        //=================PATIENTS CARE=================

        //====================FINANCE====================
        public IActionResult Billing()
        {
            return View();
        }

        public IActionResult Payments()
        {
            return View();
        }
        //====================FINANCE====================

        //===================Inventory===================
        public IActionResult DentalSupplies()
        {
            return View();
        }
        
        public IActionResult Stocks()
        {
            return View();
        }
        //===================Inventory===================

        [Authorize]
        public IActionResult Dashboard()
        {
            return View();
        }


        [Authorize]
        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
