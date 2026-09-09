using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DentalSync.Controllers
{
    [AllowAnonymous]
    public class LandingPageController : Controller
    {
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
    }
}
