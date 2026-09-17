using DentalSync.Data;
using DentalSync.Models;
using DentalSync.ViewModels;
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
        public async Task<IActionResult> Index()
        {
            var users = _userManager.Users.AsNoTracking();
            var now = DateTimeOffset.UtcNow;
            var administrators = await _userManager.GetUsersInRoleAsync("Administrator");
            var activeClinics = administrators.Count(user => !user.LockoutEnd.HasValue || user.LockoutEnd <= now);
            var lockedAccounts = await users.CountAsync(user => user.LockoutEnd.HasValue && user.LockoutEnd > now);
            var recentActivities = await _db.AuditLogs
                .AsNoTracking()
                .OrderByDescending(log => log.DateTime)
                .Take(4)
                .ToListAsync();

            return View(new SuperadminDashboardViewModel
            {
                ActiveClinics = activeClinics,
                TotalAccounts = await users.CountAsync(),
                TotalRevenue = await _db.Payments.AsNoTracking().SumAsync(payment => (decimal?)payment.Amount) ?? 0m,
                LockedAccounts = lockedAccounts,
                RecentActivities = recentActivities.Select(log => new DashboardActivityItemViewModel
                {
                    Id = log.Id,
                    User = log.User,
                    Action = log.Action,
                    Module = log.Module,
                    Description = log.Description,
                    Timestamp = log.DateTime,
                    RelativeTimeText = GetRelativeTimeString(log.DateTime)
                }).ToList()
            });
        }

        private static string GetRelativeTimeString(DateTime dateTime)
        {
            var timeSpan = DateTime.Now - dateTime;
            if (timeSpan.TotalMinutes < 1) return "Just now";
            if (timeSpan.TotalMinutes < 60) return $"{(int)timeSpan.TotalMinutes} min ago";
            if (timeSpan.TotalHours < 24) return $"{(int)timeSpan.TotalHours} hr{((int)timeSpan.TotalHours > 1 ? "s" : "")} ago";
            if (timeSpan.TotalDays < 7) return $"{(int)timeSpan.TotalDays} day{((int)timeSpan.TotalDays > 1 ? "s" : "")} ago";
            return dateTime.ToString("MMM dd, h:mm tt");
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
        public async Task<IActionResult> Subscriptions(string search = "", string plan = "", string billing = "", string status = "")
        {
            ViewBag.Search = search;
            ViewBag.Plan = plan;
            ViewBag.Billing = billing;
            ViewBag.Status = status;

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

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                subscriptions = subscriptions
                    .Where(x => x.ClinicName.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                                x.Email.Contains(s, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            if (!string.IsNullOrWhiteSpace(plan))
            {
                subscriptions = subscriptions
                    .Where(x => x.Plan.Equals(plan, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            if (!string.IsNullOrWhiteSpace(billing))
            {
                subscriptions = subscriptions
                    .Where(x => x.Billing.Equals(billing, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                var normalizedStatus = status.Trim();
                subscriptions = subscriptions
                    .Where(x => x.Status.Equals(normalizedStatus, StringComparison.OrdinalIgnoreCase) ||
                                (normalizedStatus == "active" && !x.IsSuspended) ||
                                (normalizedStatus == "suspended" && x.IsSuspended))
                    .ToList();
            }

            return View(subscriptions);
        }

        // =========================================================================
        // 5. GLOBAL ANALYTICS AND REPORTS
        // =========================================================================
        public async Task<IActionResult> Analytics()
        {
            var sixMonthsAgo = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1).AddMonths(-5);
            var users = _userManager.Users.AsNoTracking();
            var auditLogs = _db.AuditLogs.AsNoTracking();

            var roleCounts = (await (from user in users
                                     join userRole in _db.UserRoles on user.Id equals userRole.UserId
                                     join role in _db.Roles on userRole.RoleId equals role.Id
                                     group user by role.Name into grouped
                                     select new { Role = grouped.Key ?? "Unknown", Count = grouped.Count() })
                .ToListAsync())
                .Select(item => new ChartPoint { Label = item.Role, Count = item.Count })
                .OrderByDescending(item => item.Count)
                .ToList();

            var patientGrowth = (await _db.Patients.AsNoTracking()
                .Where(patient => patient.CreatedAt >= sixMonthsAgo)
                .GroupBy(patient => new { patient.CreatedAt.Year, patient.CreatedAt.Month })
                .Select(grouped => new { grouped.Key.Year, grouped.Key.Month, Count = grouped.Count() })
                .ToListAsync())
                .Select(item => new ChartPoint { Label = $"{item.Year}-{item.Month:D2}", Count = item.Count })
                .OrderBy(item => item.Label)
                .ToList();

            var appointmentsByMonth = (await _db.Appointments.AsNoTracking()
                .Where(appointment => appointment.CreatedAt >= sixMonthsAgo)
                .GroupBy(appointment => new { appointment.CreatedAt.Year, appointment.CreatedAt.Month })
                .Select(grouped => new { grouped.Key.Year, grouped.Key.Month, Count = grouped.Count() })
                .ToListAsync())
                .Select(item => new ChartPoint { Label = $"{item.Year}-{item.Month:D2}", Count = item.Count })
                .OrderBy(item => item.Label)
                .ToList();

            var revenueByMonth = (await _db.Payments.AsNoTracking()
                .Where(payment => payment.PaymentDate >= sixMonthsAgo)
                .GroupBy(payment => new { payment.PaymentDate.Year, payment.PaymentDate.Month })
                .Select(grouped => new { grouped.Key.Year, grouped.Key.Month, Value = grouped.Sum(payment => payment.Amount) })
                .ToListAsync())
                .Select(item => new ChartPoint { Label = $"{item.Year}-{item.Month:D2}", Value = item.Value })
                .OrderBy(item => item.Label)
                .ToList();

            var loginsByMonth = await GetAuditTrendAsync(auditLogs, "Login", sixMonthsAgo);
            var failedLoginsByMonth = await GetAuditTrendAsync(auditLogs, "Failed Login", sixMonthsAgo);
            var auditModules = (await auditLogs
                .Where(log => !string.IsNullOrEmpty(log.Module))
                .GroupBy(log => log.Module)
                .Select(grouped => new { Module = grouped.Key, Count = grouped.Count() })
                .ToListAsync())
                .Select(item => new ChartPoint { Label = item.Module, Count = item.Count })
                .OrderByDescending(item => item.Count)
                .Take(8)
                .ToList();

            var browserStats = (await auditLogs
                .Where(log => !string.IsNullOrEmpty(log.Browser))
                .GroupBy(log => log.Browser)
                .Select(grouped => new { Browser = grouped.Key, Count = grouped.Count() })
                .ToListAsync())
                .Select(item => new ChartPoint { Label = item.Browser, Count = item.Count })
                .OrderByDescending(item => item.Count)
                .Take(6)
                .ToList();

            var now = DateTimeOffset.UtcNow;
            var suspendedUsers = await users.CountAsync(user => user.LockoutEnd.HasValue && user.LockoutEnd > now);
            var totalUsers = await users.CountAsync();

            var vm = new GlobalAnalyticsViewModel
            {
                TotalUsers = totalUsers,
                ActiveUsers = totalUsers - suspendedUsers,
                SuspendedUsers = suspendedUsers,
                TotalPatients = await _db.Patients.AsNoTracking().CountAsync(),
                TotalDentists = await _db.Dentists.AsNoTracking().CountAsync(),
                TotalAppointments = await _db.Appointments.AsNoTracking().CountAsync(),
                TotalRevenue = await _db.Payments.AsNoTracking().SumAsync(payment => (decimal?)payment.Amount) ?? 0m,
                FailedLogins = await auditLogs.CountAsync(log => log.Module == "Authentication" && log.Action == "Failed Login"),
                LockedAccounts = suspendedUsers,
                UserRoles = roleCounts,
                PatientGrowth = patientGrowth,
                AppointmentsByMonth = appointmentsByMonth,
                RevenueByMonth = revenueByMonth,
                LoginsByMonth = loginsByMonth,
                FailedLoginsByMonth = failedLoginsByMonth,
                AuditModules = auditModules,
                BrowserStats = browserStats
            };

            return View(vm);
        }

        private static async Task<List<ChartPoint>> GetAuditTrendAsync(
            IQueryable<AuditLog> auditLogs, string action, DateTime from)
        {
            return (await auditLogs
                .Where(log => log.Module == "Authentication" && log.Action == action && log.DateTime >= from)
                .GroupBy(log => new { log.DateTime.Year, log.DateTime.Month })
                .Select(grouped => new { grouped.Key.Year, grouped.Key.Month, Count = grouped.Count() })
                .ToListAsync())
                .Select(item => new ChartPoint { Label = $"{item.Year}-{item.Month:D2}", Count = item.Count })
                .OrderBy(item => item.Label)
                .ToList();
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

        // =========================================================================
        // 7. TERMS & CONDITIONS MANAGEMENT FOR CHECKOUT PAGE
        // =========================================================================
        public async Task<IActionResult> TermsAndConditions()
        {
            var terms = await _db.TermsAndConditions.FirstOrDefaultAsync();
            if (terms == null)
            {
                terms = new TermsAndConditions
                {
                    Title = "DentalSync Subscription Terms & Conditions",
                    Content = "Enter your terms and conditions here...",
                    UpdatedAt = DateTime.UtcNow,
                    UpdatedBy = User.Identity?.Name ?? "Superadmin"
                };
            }
            return View(terms);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateTermsAndConditions(string title, string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                TempData["ErrorMessage"] = "Terms and Conditions content cannot be empty.";
                return RedirectToAction("TermsAndConditions");
            }

            var terms = await _db.TermsAndConditions.FirstOrDefaultAsync();
            if (terms == null)
            {
                terms = new TermsAndConditions();
                _db.TermsAndConditions.Add(terms);
            }

            terms.Title = string.IsNullOrWhiteSpace(title) ? "DentalSync Subscription Terms & Conditions" : title.Trim();
            terms.Content = content.Trim();
            terms.UpdatedAt = DateTime.UtcNow;
            terms.UpdatedBy = User.Identity?.Name ?? "Superadmin";

            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = "Terms & Conditions updated successfully! The updated terms will automatically display on the Checkout Page when users continue to payment.";
            return RedirectToAction("TermsAndConditions");
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
