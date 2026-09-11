using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using DentalSync.Data;
using DentalSync.Models;
using DentalSync.Services;

namespace DentalSync.Controllers
{
    [AllowAnonymous]
    public class LandingPageController : Controller
    {
        private readonly UserManager<Users> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IPayMongoService _payMongoService;
        private readonly ILogger<LandingPageController> _logger;
        private readonly AppDbContext _db;

        public LandingPageController(
            UserManager<Users> userManager,
            RoleManager<IdentityRole> roleManager,
            IPayMongoService payMongoService,
            ILogger<LandingPageController> logger,
            AppDbContext db)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _payMongoService = payMongoService;
            _logger = logger;
            _db = db;
        }

        // =========================================================================
        // LANDING PAGE & PROMOTIONAL ROUTES
        // =========================================================================

        public IActionResult Promotionpage()
        {
            return View("~/Views/LandingPage/Promotionpage.cshtml");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitMessage(string name, string email, string phone, DateTime? preferredDate, string message)
        {
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(phone) || string.IsNullOrWhiteSpace(message))
            {
                TempData["ContactError"] = "Please fill in all required fields (Name, Email, Phone, and Message).";
                return Redirect($"{Url.Action("Promotionpage")}#contact");
            }

            var promoMessage = new PromotionalMessage
            {
                Name = name.Trim(),
                Email = email.Trim(),
                Phone = phone.Trim(),
                PreferredDate = preferredDate,
                Message = message.Trim(),
                Status = "New",
                CreatedAt = DateTime.UtcNow
            };

            _db.PromotionalMessages.Add(promoMessage);
            await _db.SaveChangesAsync();

            TempData["ContactSuccess"] = "Thank you for reaching out! Your message has been sent successfully. Our clinic team will get back to you shortly.";
            return Redirect($"{Url.Action("Promotionpage")}#contact");
        }

        public IActionResult SubscriptionPage()
        {
            return View("~/Views/LandingPage/SubscriptionPage.cshtml");
        }

        public async Task<IActionResult> CheckoutPage(string plan = "Starter Clinic", string billing = "monthly", string price = "799")
        {
            ViewBag.Plan = plan;
            ViewBag.Billing = billing;
            ViewBag.Price = price;

            // Retrieve Terms & Conditions set by Superadmin
            var terms = await _db.TermsAndConditions.FirstOrDefaultAsync();
            ViewBag.TermsTitle = terms?.Title ?? "DentalSync Subscription Terms & Conditions";
            ViewBag.TermsContent = terms?.Content ?? "By subscribing to DentalSync, you agree to our standard terms of service, privacy policy, and subscription guidelines.";
            ViewBag.TermsUpdatedAt = terms?.UpdatedAt.ToString("MMMM dd, yyyy") ?? DateTime.UtcNow.ToString("MMMM dd, yyyy");

            return View("~/Views/LandingPage/CheckoutPage.cshtml");
        }

        // =========================================================================
        // PAYMONGO CHECKOUT SESSION & ADMIN REGISTRATION
        // =========================================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateCheckoutSession(
            string fullName,
            string email,
            string password,
            string plan,
            string billing,
            string price,
            string paymentMethod = "card")
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

            decimal priceVal = 799;
            decimal.TryParse(price, out priceVal);

            // Store registration info temporarily in TempData for account activation upon successful payment return
            TempData["Pending_FullName"] = fullName;
            TempData["Pending_Email"]    = email;
            TempData["Pending_Password"] = password;
            TempData["Pending_Plan"]     = plan;
            TempData["Pending_Billing"]  = billing;

            string successUrl = Url.Action("PaymentSuccess", "LandingPage", null, Request.Scheme) ?? "";
            string cancelUrl  = Url.Action("CheckoutPage", "LandingPage", new { plan, billing, price }, Request.Scheme) ?? "";

            var session = await _payMongoService.CreateCheckoutSessionAsync(
                priceVal,
                plan,
                billing,
                fullName,
                email,
                successUrl,
                cancelUrl,
                paymentMethod);

            if (session?.Data?.Attributes?.CheckoutUrl != null && !string.IsNullOrEmpty(session.Data.Attributes.CheckoutUrl))
            {
                // Redirect user to PayMongo secure payment page (Card, GCash, Maya)
                return Redirect(session.Data.Attributes.CheckoutUrl);
            }

            // If PayMongo API returns null (e.g., test API key unconfigured), fallback to direct registration
            _logger.LogWarning("PayMongo checkout session creation returned null or unconfigured key. Proceeding with registration fallback.");
            return await CompleteAdminRegistration(fullName, email, password);
        }

        [HttpGet]
        public async Task<IActionResult> PaymentSuccess(string? session_id)
        {
            if (!string.IsNullOrEmpty(session_id))
            {
                var session = await _payMongoService.GetCheckoutSessionAsync(session_id);
                _logger.LogInformation("Payment callback received for session {SessionId}, Status: {Status}", session_id, session?.Data?.Attributes?.Status);
            }

            string? fullName = TempData["Pending_FullName"] as string;
            string? email    = TempData["Pending_Email"] as string;
            string? password = TempData["Pending_Password"] as string;

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                TempData["RegisterSuccess"] = "true";
                return RedirectToAction("Login", "Account");
            }

            return await CompleteAdminRegistration(fullName ?? email, email, password);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegisterAdmin(
            string fullName,
            string email,
            string password,
            string plan,
            string billing,
            string price,
            string paymentMethod = "card")
        {
            return await CreateCheckoutSession(fullName, email, password, plan, billing, price, paymentMethod);
        }

        private async Task<IActionResult> CompleteAdminRegistration(string fullName, string email, string password)
        {
            if (!await _roleManager.RoleExistsAsync("Administrator"))
            {
                await _roleManager.CreateAsync(new IdentityRole("Administrator"));
            }

            var existing = await _userManager.FindByEmailAsync(email);
            if (existing != null)
            {
                TempData["RegisterSuccess"] = "true";
                return RedirectToAction("Login", "Account");
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

            TempData["RegisterError"] = string.Join(" ", result.Errors.Select(e => e.Description));
            return RedirectToAction("CheckoutPage");
        }
    }
}
