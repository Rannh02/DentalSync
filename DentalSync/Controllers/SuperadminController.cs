using DentalSync.Data;
using DentalSync.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DentalSync.Controllers
{
    [Authorize]
    public class SuperadminController : Controller
    {
        private readonly AppDbContext _db;
        private readonly UserManager<Users> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public SuperadminController(
            AppDbContext db,
            UserManager<Users> userManager,
            RoleManager<IdentityRole> roleManager)
        {
            _db = db;
            _userManager = userManager;
            _roleManager = roleManager;
        }

        // =========================================================================
        // 1. DASHBOARD OVERVIEW
        // =========================================================================
        public IActionResult Index()
        {
            return View();
        }

        // =========================================================================
        // 2. USER MANAGEMENT (SEPARATED TABLES BY DATABASE ROLES)
        // =========================================================================
        public async Task<IActionResult> UserManagement(string search = "", string role = "", string status = "")
        {
            ViewBag.Search = search;
            ViewBag.Role = role;
            ViewBag.Status = status;

            var allUsers = await _userManager.Users.ToListAsync();

            var vm = new SuperadminUserManagementViewModel();

            foreach (var u in allUsers)
            {
                var roles = await _userManager.GetRolesAsync(u);
                var userRole = roles.FirstOrDefault() ?? "Patient";
                var isSuspended = u.LockoutEnd.HasValue && u.LockoutEnd.Value > DateTimeOffset.UtcNow;

                var item = new SuperadminUserViewModel
                {
                    UserId = u.Id,
                    FullName = string.IsNullOrWhiteSpace(u.FullName) ? (u.UserName ?? u.Email ?? "User") : u.FullName,
                    Email = u.Email ?? "",
                    Phone = u.PhoneNumber ?? "N/A",
                    Role = userRole,
                    IsSuspended = isSuspended
                };

                if (userRole.Equals("Superadmin", StringComparison.OrdinalIgnoreCase))
                {
                    item.ExtraInfo = "Full System Control";
                    vm.Superadmins.Add(item);
                }
                else if (userRole.Equals("Administrator", StringComparison.OrdinalIgnoreCase))
                {
                    item.ExtraInfo = "Main Clinic Operations";
                    vm.Administrators.Add(item);
                }
                else if (userRole.Equals("Dentist", StringComparison.OrdinalIgnoreCase))
                {
                    var dentistProfile = await _db.Dentists.FirstOrDefaultAsync(d => d.UserId == u.Id);
                    item.ExtraInfo = dentistProfile?.Specialization ?? "General Dentistry";
                    vm.Dentists.Add(item);
                }
                else if (userRole.Equals("Receptionist", StringComparison.OrdinalIgnoreCase))
                {
                    item.ExtraInfo = "Front Desk & Scheduling";
                    vm.Receptionists.Add(item);
                }
                else // Patient
                {
                    var patientProfile = await _db.Patients.FirstOrDefaultAsync(p => p.UserId == u.Id);
                    item.ExtraInfo = patientProfile?.ContactNumber ?? u.PhoneNumber ?? "N/A";
                    vm.Patients.Add(item);
                }
            }

            // Fallback sample data if DB is empty for demonstration
            EnsureDemoData(vm);

            // Apply search & status filters specifically to Administrators if requested
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                vm.Administrators = vm.Administrators
                    .Where(a => (a.FullName != null && a.FullName.Contains(s, StringComparison.OrdinalIgnoreCase)) ||
                                (a.Email != null && a.Email.Contains(s, StringComparison.OrdinalIgnoreCase)))
                    .ToList();
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                if (status.Equals("active", StringComparison.OrdinalIgnoreCase))
                {
                    vm.Administrators = vm.Administrators.Where(a => !a.IsSuspended).ToList();
                }
                else if (status.Equals("inactive", StringComparison.OrdinalIgnoreCase) || status.Equals("suspended", StringComparison.OrdinalIgnoreCase))
                {
                    vm.Administrators = vm.Administrators.Where(a => a.IsSuspended).ToList();
                }
            }

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleSuspend(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId) ?? await _userManager.FindByEmailAsync(userId);
            if (user != null)
            {
                var isSuspended = user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow;
                if (isSuspended)
                {
                    await _userManager.SetLockoutEndDateAsync(user, null);
                    TempData["SuccessMessage"] = $"Account access for '{user.FullName ?? user.Email}' has been restored.";
                }
                else
                {
                    await _userManager.SetLockoutEnabledAsync(user, true);
                    await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddYears(100));
                    TempData["SuccessMessage"] = $"Account '{user.FullName ?? user.Email}' has been suspended.";
                }
            }
            else
            {
                TempData["SuccessMessage"] = $"Updated account status for user.";
            }

            return RedirectToAction("UserManagement");
        }

        // Helper to ensure rich demo data if DB contains minimal entries
        private void EnsureDemoData(SuperadminUserManagementViewModel vm)
        {
            if (!vm.Superadmins.Any())
            {
                vm.Superadmins.Add(new SuperadminUserViewModel
                {
                    UserId = "sa-1", FullName = "System Administrator", Email = "superadmin@dentalsync.ph", Phone = "+63 (082) 221-0190", Role = "Superadmin", ExtraInfo = "Full Platform Root", IsSuspended = false
                });
            }

            if (!vm.Administrators.Any())
            {
                vm.Administrators.Add(new SuperadminUserViewModel
                {
                    UserId = "admin-1", FullName = "Ralph David Milla Soliva", Email = "ralph@clinic.ph", Phone = "+63 (082) 221-0190", Role = "Administrator", ExtraInfo = "Main Branch (Davao)", IsSuspended = false
                });
            }

            if (!vm.Dentists.Any())
            {
                vm.Dentists.Add(new SuperadminUserViewModel
                {
                    UserId = "dent-1", FullName = "Dr. Maria Santos", Email = "maria.santos@dentalsync.ph", Phone = "+63 917 111 2222", Role = "Dentist", ExtraInfo = "General Dentistry", IsSuspended = false
                });
                vm.Dentists.Add(new SuperadminUserViewModel
                {
                    UserId = "dent-2", FullName = "Dr. Damby Malupiton", Email = "damby.malupiton@dentalsync.ph", Phone = "+63 918 333 4444", Role = "Dentist", ExtraInfo = "Restorative Dentistry", IsSuspended = false
                });
                vm.Dentists.Add(new SuperadminUserViewModel
                {
                    UserId = "dent-3", FullName = "Dr. Angela Cruz", Email = "angela.cruz@dentalsync.ph", Phone = "+63 919 555 6666", Role = "Dentist", ExtraInfo = "Cosmetic Dentistry", IsSuspended = false
                });
            }

            if (!vm.Receptionists.Any())
            {
                vm.Receptionists.Add(new SuperadminUserViewModel
                {
                    UserId = "rec-1", FullName = "Juana Dela Cruz", Email = "reception@dentalsync.ph", Phone = "+63 920 777 8888", Role = "Receptionist", ExtraInfo = "Downtown Branch", IsSuspended = false
                });
                vm.Receptionists.Add(new SuperadminUserViewModel
                {
                    UserId = "rec-2", FullName = "Pedro Penduko", Email = "reception.north@dentalsync.ph", Phone = "+63 921 999 0000", Role = "Receptionist", ExtraInfo = "North Branch", IsSuspended = false
                });
            }

            if (!vm.Patients.Any())
            {
                vm.Patients.Add(new SuperadminUserViewModel
                {
                    UserId = "pat-1", FullName = "José Rizal", Email = "jose.rizal@gmail.com", Phone = "+63 917 123 4567", Role = "Patient", ExtraInfo = "Main Branch (Davao)", IsSuspended = true
                });
                vm.Patients.Add(new SuperadminUserViewModel
                {
                    UserId = "pat-2", FullName = "Juan Luna", Email = "juan.luna@gmail.com", Phone = "+63 918 234 5678", Role = "Patient", ExtraInfo = "North Branch", IsSuspended = false
                });
                vm.Patients.Add(new SuperadminUserViewModel
                {
                    UserId = "pat-3", FullName = "Andres Bonifacio", Email = "andres.b@gmail.com", Phone = "+63 919 345 6789", Role = "Patient", ExtraInfo = "Downtown Branch", IsSuspended = false
                });
            }
        }

        // =========================================================================
        // 3. MULTI-BRANCH & CLINIC SETTINGS
        // =========================================================================
        public IActionResult ClinicSettings()
        {
            return View();
        }

        // =========================================================================
        // 4. SUBSCRIPTION AND BILLING (CLINIC SUBSCRIPTION TABLE)
        // =========================================================================
        public async Task<IActionResult> Subscriptions(string search = "")
        {
            ViewBag.Search = search;

            var allUsers = await _userManager.Users.ToListAsync();
            var subscriptions = new List<SubscriptionViewModel>();

            foreach (var u in allUsers)
            {
                var roles = await _userManager.GetRolesAsync(u);
                if (!roles.Contains("Administrator")) continue;

                var isSuspended = u.LockoutEnd.HasValue && u.LockoutEnd.Value > DateTimeOffset.UtcNow;
                subscriptions.Add(new SubscriptionViewModel
                {
                    UserId    = u.Id,
                    ClinicName = string.IsNullOrWhiteSpace(u.FullName) ? (u.UserName ?? u.Email ?? "Unknown") : u.FullName,
                    Email     = u.Email ?? "",
                    Plan      = "Starter Clinic",
                    Billing   = "Monthly",
                    Status    = isSuspended ? "Suspended" : "Active",
                    IsSuspended = isSuspended
                });
            }

            // Apply search filter
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                subscriptions = subscriptions
                    .Where(x => x.ClinicName.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                                x.Email.Contains(s, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            return View(subscriptions);
        }

        // =========================================================================
        // 5. GLOBAL ANALYTICS AND REPORTS
        // =========================================================================
        public IActionResult Analytics()
        {
            return View();
        }

        // =========================================================================
        // 6. SYSTEM AUDIT LOGS & SECURITY
        // =========================================================================
        public IActionResult AuditLogs(string search = "", string role = "")
        {
            ViewBag.Search = search;
            ViewBag.Role = role;
            return View();
        }
    }

    // ViewModels for Superadmin User Management
    public class SuperadminUserViewModel
    {
        public string UserId { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string ExtraInfo { get; set; } = string.Empty;
        public bool IsSuspended { get; set; }
    }

    public class SuperadminUserManagementViewModel
    {
        public List<SuperadminUserViewModel> Superadmins { get; set; } = new();
        public List<SuperadminUserViewModel> Administrators { get; set; } = new();
        public List<SuperadminUserViewModel> Dentists { get; set; } = new();
        public List<SuperadminUserViewModel> Receptionists { get; set; } = new();
        public List<SuperadminUserViewModel> Patients { get; set; } = new();

        public int TotalCount => Superadmins.Count + Administrators.Count + Dentists.Count + Receptionists.Count + Patients.Count;
    }

    public class SubscriptionViewModel
    {
        public string UserId { get; set; } = string.Empty;
        public string ClinicName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Plan { get; set; } = string.Empty;
        public string Billing { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public bool IsSuspended { get; set; }
    }
}
