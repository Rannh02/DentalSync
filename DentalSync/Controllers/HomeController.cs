using DentalSync.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using Microsoft.AspNetCore.Identity;
using DentalSync.ViewModels;

namespace DentalSync.Controllers
{
    public class HomeController : Controller
    {
        private readonly UserManager<Users> userManager;

        public HomeController(UserManager<Users> userManager)
        {
            this.userManager = userManager;
        }

        //=================ADMINISTRATOR=================
        public IActionResult Users()
        {
            return RedirectToAction("Users", "Users");
        }
        public IActionResult Services()
        {
            return View();
        }
        //=================ADMINISTRATOR=================

        public IActionResult Audit()
        {
            return View();
        }
        public IActionResult Authentication()
        {
            return View();
        }
        public IActionResult RolePerm()
        {
            return RedirectToAction("Index", "RolePermissions");
        }
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
        public async Task<IActionResult> Dashboard()
        {
            var user = await userManager.GetUserAsync(User);
            ViewBag.UserFullName = user?.FullName ?? user?.UserName ?? "there";
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
