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
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;

        public AccountController(SignInManager<Users> signInManager, UserManager<Users> userManager, AuditService audit, IConfiguration configuration, IHttpClientFactory httpClientFactory)
        {
            this.signInManager = signInManager;
            this.userManager = userManager;
            _audit = audit;
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;
        }



        //==================================================================================
        //=====================================LOGIN=======================================
        //==================================================================================
        public async Task<IActionResult> Login()
        {
            await EnsureSuperadminSeededAsync();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string returnUrl = null)
        {
            await EnsureSuperadminSeededAsync();

            var user = await userManager.FindByEmailAsync(model.Email)
                ?? await userManager.FindByNameAsync(model.Email);

            var userName = user?.FullName ?? user?.UserName ?? (string.IsNullOrWhiteSpace(model.Email) ? "Unknown" : model.Email);
            var roles = user != null ? await userManager.GetRolesAsync(user) : new List<string>();
            var roleName = roles.FirstOrDefault() ?? "Unknown";

            // 1. Check if user account is currently locked out
            if (user != null)
            {
                if (!await userManager.GetLockoutEnabledAsync(user))
                {
                    await userManager.SetLockoutEnabledAsync(user, true);
                }

                if (await userManager.IsLockedOutAsync(user))
                {
                    var lockoutEnd = await userManager.GetLockoutEndDateAsync(user);
                    var remainingMinutes = lockoutEnd.HasValue
                        ? Math.Max(1, (int)Math.Ceiling((lockoutEnd.Value - DateTimeOffset.UtcNow).TotalMinutes))
                        : 5;

                    await _audit.LogAsync("Account Locked", "Authentication",
                        $"Login blocked - Account is locked out for {remainingMinutes} more minute(s) due to multiple failed login attempts for: {model.Email}",
                        overrideUser: userName, overrideRole: roleName);

                    ModelState.AddModelError(string.Empty, $"Your account has been temporarily locked for 5 minutes due to 3 failed login attempts. Please try again after {remainingMinutes} minute(s).");
                    return View(model);
                }
            }

            // 2. Validate Google reCAPTCHA
            var recaptchaResponse = Request.Form["g-recaptcha-response"].ToString();
            var secretKey = _configuration["ReCaptcha:SecretKey"];

            if (!string.IsNullOrEmpty(secretKey))
            {
                if (string.IsNullOrWhiteSpace(recaptchaResponse))
                {
                    // Record in Audit Logs when user tries to login without checking reCAPTCHA
                    await _audit.LogAsync("Failed Login", "Authentication",
                        $"Failed login attempt - reCAPTCHA checkbox was not completed for: {model.Email}",
                        overrideUser: userName, overrideRole: roleName);

                    ModelState.AddModelError(string.Empty, "Please verify that you are not a robot.");
                    return View(model);
                }

                try
                {
                    var httpClient = _httpClientFactory.CreateClient();
                    var postContent = new FormUrlEncodedContent(new[]
                    {
                        new KeyValuePair<string, string>("secret", secretKey),
                        new KeyValuePair<string, string>("response", recaptchaResponse),
                        new KeyValuePair<string, string>("remoteip", HttpContext.Connection.RemoteIpAddress?.ToString() ?? "")
                    });

                    var verifyResult = await httpClient.PostAsync("https://www.google.com/recaptcha/api/siteverify", postContent);
                    if (verifyResult.IsSuccessStatusCode)
                    {
                        var json = await verifyResult.Content.ReadAsStringAsync();
                        using var doc = System.Text.Json.JsonDocument.Parse(json);
                        if (doc.RootElement.TryGetProperty("success", out var successProp) && !successProp.GetBoolean())
                        {
                            await _audit.LogAsync("Failed Login", "Authentication",
                                $"Failed login attempt - reCAPTCHA verification failed for: {model.Email}",
                                overrideUser: userName, overrideRole: roleName);

                            ModelState.AddModelError(string.Empty, "reCAPTCHA verification failed. Please try again.");
                            return View(model);
                        }
                    }
                }
                catch
                {
                    // Fallback gracefully on network timeout
                }
            }

            // 3. Process Login Authentication
            if (ModelState.IsValid)
            {
                var result = user == null
                    ? Microsoft.AspNetCore.Identity.SignInResult.Failed
                    : await signInManager.PasswordSignInAsync(user.UserName ?? model.Email, model.Password, model.RememberMe, lockoutOnFailure: true);

                if (result.Succeeded)
                {
                    await _audit.LogAsync("Login", "Authentication", $"Successful login",
                        overrideUser: userName, overrideRole: roleName);

                    if (user != null && (await userManager.IsInRoleAsync(user, "Superadmin") || string.Equals(user.Email, "superadmin@dentalsync.ph", StringComparison.OrdinalIgnoreCase)))
                    {
                        return RedirectToAction("Index", "Superadmin");
                    }
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

                    // Sanitize returnUrl: only redirect to local URLs
                    if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                    {
                        try
                        {
                            var decoded = System.Uri.UnescapeDataString(returnUrl ?? string.Empty);
                            if (!decoded.Contains(':'))
                            {
                                return Redirect(returnUrl);
                            }
                        }
                        catch
                        {
                            // Fall back to safe redirect
                        }
                    }

                    return RedirectToAction("Dashboard", "Home");
                }
                else if (result.IsLockedOut)
                {
                    // Record account lockout event in Audit Logs
                    await _audit.LogAsync("Account Locked", "Authentication",
                        $"Account temporarily locked for 5 minutes after 3 failed login attempts for: {model.Email}",
                        overrideUser: userName, overrideRole: roleName);

                    ModelState.AddModelError(string.Empty, "Your account has been locked for 5 minutes due to 3 failed login attempts. Please try again after 5 minutes.");
                    return View(model);
                }
                else
                {
                    // Refresh user entity to get updated AccessFailedCount after PasswordSignInAsync
                    var updatedUser = user != null ? (await userManager.FindByEmailAsync(model.Email) ?? await userManager.FindByNameAsync(model.Email)) : null;
                    var failedCount = updatedUser != null ? await userManager.GetAccessFailedCountAsync(updatedUser) : 1;
                    var remaining = Math.Max(0, 3 - failedCount);

                    await _audit.LogAsync("Failed Login", "Authentication",
                        $"Failed login attempt for {userName} (Failed attempt {failedCount}/3)",
                        overrideUser: userName, overrideRole: roleName);

                    ModelState.AddModelError(string.Empty, "Email or password is incorrect.");

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

        private async Task EnsureSuperadminSeededAsync()
        {
            try
            {
                var roleManager = HttpContext.RequestServices.GetService<RoleManager<IdentityRole>>();
                if (roleManager != null && !await roleManager.RoleExistsAsync("Superadmin"))
                {
                    await roleManager.CreateAsync(new IdentityRole("Superadmin"));
                }

                const string email = "superadmin@dentalsync.ph";
                const string password = "Superadmin@123";

                var user = await userManager.FindByEmailAsync(email) ?? await userManager.FindByNameAsync(email);
                if (user == null)
                {
                    user = new Users
                    {
                        FullName = "Super Admin",
                        UserName = email,
                        Email = email,
                        EmailConfirmed = true,
                        LockoutEnabled = true
                    };

                    var res = await userManager.CreateAsync(user, password);
                    if (res.Succeeded && roleManager != null)
                    {
                        await userManager.AddToRoleAsync(user, "Superadmin");
                    }
                }
                else
                {
                    user.EmailConfirmed = true;
                    if (!user.LockoutEnabled)
                    {
                        user.LockoutEnabled = true;
                        await userManager.UpdateAsync(user);
                    }

                    if (roleManager != null && !await userManager.IsInRoleAsync(user, "Superadmin"))
                    {
                        await userManager.AddToRoleAsync(user, "Superadmin");
                    }

                    var token = await userManager.GeneratePasswordResetTokenAsync(user);
                    await userManager.ResetPasswordAsync(user, token, password);
                }
            }
            catch
            {
                // Seeding exception handler
            }
        }
    }
}
