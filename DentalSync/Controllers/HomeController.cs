using DentalSync.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;

namespace DentalSync.Controllers
{
    public class HomeController : Controller
    {

        //=================ADMINISTRATOR=================
        public IActionResult Users()
        {
            return View();
        }

        public IActionResult Services()
        {
            return View();
        }
        //=================ADMINISTRATOR=================



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
        public IActionResult Records()
        {
            return View();
        }
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
