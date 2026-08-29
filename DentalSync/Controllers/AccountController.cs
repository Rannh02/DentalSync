using DentalSync.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using DentalSync.Models;
using DentalSync.Services;

namespace DentalSync.Controllers
{
    public class AccountController : Controller
    {
        private readonly SignInManager<Users> signInManager;
        private readonly UserManager<Users> userManager;
        private readonly AuditService _audit;

        public AccountController(SignInManager<Users> signInManager, UserManager<Users> userManager, AuditService audit)
        {
            this.signInManager = signInManager;
            this.userManager = userManager;
            _audit = audit;
        }



        //==================================================================================
        //=====================================LOGIN=======================================
        //==================================================================================
        public IActionResult Login()
        {
            return View();
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string returnUrl = null)
        {
            if (ModelState.IsValid)
            {
                var user = await userManager.FindByEmailAsync(model.Email)
                    ?? await userManager.FindByNameAsync(model.Email);
                var result = user == null
                    ? Microsoft.AspNetCore.Identity.SignInResult.Failed
                    : await signInManager.PasswordSignInAsync(user.UserName ?? model.Email, model.Password, model.RememberMe, false);

                if (result.Succeeded)
                {
                    var roles = user != null ? await userManager.GetRolesAsync(user) : new List<string>();
                    var roleName = roles.FirstOrDefault() ?? "Unknown";
                    var displayName = user?.FullName ?? user?.UserName ?? model.Email;
                    await _audit.LogAsync("Login", "Authentication", $"Successful login",
                        overrideUser: displayName, overrideRole: roleName);

                    if (user != null && await userManager.IsInRoleAsync(user, "Receptionist"))
                    {
                        return RedirectToAction("Receptionist_Dashboard", "Receptionist");
                    }
                    if (user != null && await userManager.IsInRoleAsync(user, "Patient"))
                    {
                        return RedirectToAction("Dashboard", "Patient");
                    }
                    if (user != null && await userManager.IsInRoleAsync(user, "Dentist"))
                    {
                        return RedirectToAction("Dashboard", "Dentist");
                    }

                    // Sanitize returnUrl: only redirect to local URLs and avoid suspicious values like trailing ':' or embedded scheme
                    if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                    {
                        try
                        {
                            var decoded = System.Uri.UnescapeDataString(returnUrl ?? string.Empty);
                            // Reject urls containing a colon to avoid scheme-like values such as "Home:"
                            if (!decoded.Contains(':'))
                            {
                                return Redirect(returnUrl);
                            }
                        }
                        catch
                        {
                            // Ignore decoding errors and fall back to safe redirect
                        }
                    }

                    return RedirectToAction("Dashboard", "Home");
                }
                else
                {
                    // Log failed login
                    var userName = user?.FullName ?? user?.UserName ?? model.Email;
                    var roles = user != null ? await userManager.GetRolesAsync(user) : new List<string>();
                    var roleName = roles.FirstOrDefault() ?? "Unknown";
                    await _audit.LogAsync("Failed Login", "Authentication", $"Failed login attempt for {userName}",
                        overrideUser: userName, overrideRole: roleName);

                    ModelState.AddModelError("", "Email or password is incorrect");
                    return View(model);
                }
            }
            return View(model);
        }
        //==================================================================================
        //=====================================LOGIN=======================================
        //==================================================================================









        //==================================================================================
        //=====================================REGISTER=====================================
        //==================================================================================
        public IActionResult Register()
        {
            return RedirectToAction("Login", "Account");
        }
        //==================================================================================
        //=====================================REGISTER=====================================
        //==================================================================================




        public IActionResult VerifyEmail()
        {
            return View();
        }
        [HttpPost]
        public async Task<IActionResult> VerifyEmail(VerifyEmailViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = await userManager.FindByEmailAsync(model.Email);

                if (user == null)
                {
                    ModelState.AddModelError(string.Empty, "No user was found with that email address.");
                    return View(model);
                }
                else
                {
                    return RedirectToAction(nameof(ChangePassword), new { email = user.Email });
                }
            }
            return View(model);
        }






        public IActionResult ChangePassword(string email)
        {
            if (string.IsNullOrEmpty(email))
            {
                return RedirectToAction(nameof(VerifyEmail));
            }
            return View(new ChangePasswordViewModel { Email = email });
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = await userManager.FindByEmailAsync(model.Email);
                if (user != null)
                {
                    var token = await userManager.GeneratePasswordResetTokenAsync(user);
                    var result = await userManager.ResetPasswordAsync(user, token, model.NewPassword);
                    if (result.Succeeded)
                    {
                        return RedirectToAction(nameof(Login));
                    }
                    else
                    {
                        foreach (var error in result.Errors)
                        {
                            ModelState.AddModelError("", error.Description);
                        }
                        return View(model);
                    }
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "No user was found with that email address.");
                    return View(model);
                }
            }
            else
            {
                ModelState.AddModelError("", "Something Went Wrong! Try Again..!");
                return View(model);
            }
        }
        public IActionResult AccessDenied()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _audit.LogAsync("Logout", "Authentication", "User signed out");
            await signInManager.SignOutAsync();
            return RedirectToAction("Login", "Account");
        }
    }
}
