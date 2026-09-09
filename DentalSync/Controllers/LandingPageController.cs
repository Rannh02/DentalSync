using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using DentalSync.Models;

namespace DentalSync.Controllers
{
    [AllowAnonymous]
    public class LandingPageController : Controller
    {
        private readonly UserManager<Users> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public LandingPageController(UserManager<Users> userManager, RoleManager<IdentityRole> roleManager)
        {
            _userManager = userManager;
            _roleManager = roleManager;
        }

        // =========================================================================
        // LANDING PAGE & PROMOTIONAL ROUTES
        // =========================================================================

        public IActionResult Promotionpage()
        {
            return View("~/Views/LandingPage/Promotionpage.cshtml");
        }

        public IActionResult SubscriptionPage()
        {
            return View("~/Views/LandingPage/SubscriptionPage.cshtml");
        }

        public IActionResult CheckoutPage(string plan = "Starter Clinic", string billing = "monthly", string price = "799")
        {
            ViewBag.Plan = plan;
            ViewBag.Billing = billing;
            ViewBag.Price = price;
            return View("~/Views/LandingPage/CheckoutPage.cshtml");
        }

        // =========================================================================
        // REGISTER ADMIN ACCOUNT (called from CheckoutPage on subscription)
        // =========================================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegisterAdmin(
            string fullName,
            string email,
            string password,
            string plan,
            string billing,
            string price)
        {
            // Ensure the Administrator role exists
            if (!await _roleManager.RoleExistsAsync("Administrator"))
            {
                await _roleManager.CreateAsync(new IdentityRole("Administrator"));
            }

            // Check if email is already taken
            var existing = await _userManager.FindByEmailAsync(email);
            if (existing != null)
            {
                TempData["RegisterError"] = "An account with that email already exists. Please use a different email.";
                TempData["Plan"]    = plan;
                TempData["Billing"] = billing;
                TempData["Price"]   = price;
                return RedirectToAction("CheckoutPage", new { plan, billing, price });
            }

            var user = new Users
            {
                FullName       = fullName,
                UserName       = email,
                Email          = email,
                EmailConfirmed = true,
                LockoutEnabled = false
            };

            var result = await _userManager.CreateAsync(user, password);
            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(user, "Administrator");
                TempData["RegisterSuccess"] = "true";
                return RedirectToAction("Login", "Account");
            }

            // Return errors back to checkout
            TempData["RegisterError"] = string.Join(" ", result.Errors.Select(e => e.Description));
            TempData["Plan"]    = plan;
            TempData["Billing"] = billing;
            TempData["Price"]   = price;
            return RedirectToAction("CheckoutPage", new { plan, billing, price });
        }
    }
}

