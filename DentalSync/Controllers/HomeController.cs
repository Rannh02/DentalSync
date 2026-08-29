using DentalSync.Data;
using DentalSync.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using Microsoft.AspNetCore.Identity;
using DentalSync.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace DentalSync.Controllers
{
    public class HomeController : Controller
    {
        private readonly UserManager<Users> userManager;
        private readonly AppDbContext _db;

        public HomeController(UserManager<Users> userManager, AppDbContext db)
        {
            this.userManager = userManager;
            _db = db;
        }

        //=================ADMINISTRATOR=================
        public IActionResult Users()
        {
            return RedirectToAction("Users", "Users");
        }
        [Authorize]
        public async Task<IActionResult> Services(string search = "", string status = "", int page = 1)
        {
            return RedirectToAction("Index", "Services", new { search, status, page });
        }
        //=================ADMINISTRATOR=================

        public async Task<IActionResult> Audit(string search = "", string role = "", int page = 1)
        {
            // All roles for the dropdown
            var allRoles = new List<string> { "Administrator", "Receptionist", "Dentist", "Patient" };

            // Query from DB
            var query = _db.AuditLogs.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(l =>
                    l.User.Contains(search) ||
                    l.Action.Contains(search) ||
                    l.Module.Contains(search) ||
                    l.Description.Contains(search));

            if (!string.IsNullOrWhiteSpace(role))
                query = query.Where(l => l.Role == role);

            const int pageSize = 5;
            var total = await query.CountAsync();
            var logs  = await query
                .OrderByDescending(l => l.DateTime)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(l => new AuditLogEntry
                {
                    DateTime    = l.DateTime,
                    User        = l.User,
                    Role        = l.Role,
                    Action      = l.Action,
                    Module      = l.Module,
                    Description = l.Description,
                    IpAddress   = l.IpAddress,
                    Browser     = l.Browser,
                })
                .ToListAsync();

            var vm = new AuditLogViewModel
            {
                Search    = search,
                Role      = role,
                Roles     = allRoles,
                Logs      = logs,
                TotalLogs = total,
                Page      = page,
                PageSize  = pageSize,
            };

            return View(vm);
        }
        public async Task<IActionResult> Authentication(string search = "", string role = "", int page = 1)
        {
            var allRoles = new List<string> { "Administrator", "Receptionist", "Dentist", "Patient" };

            // Query base for authentication-related logs
            var query = _db.AuditLogs.AsNoTracking().Where(l => l.Module == "Authentication");

            // Apply filters
            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(l =>
                    l.User.Contains(search) ||
                    l.Action.Contains(search) ||
                    l.Description.Contains(search));
            }

            if (!string.IsNullOrWhiteSpace(role))
            {
                query = query.Where(l => l.Role == role);
            }

            const int pageSize = 5;
            var total = await query.CountAsync();
            var logs = await query
                .OrderByDescending(l => l.DateTime)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(l => new AuthenticationLogEntry
                {
                    DateTime = l.DateTime,
                    User = l.User,
                    Event = (l.Action == "Failed Login") ? "Login" : l.Action,
                    Status = (l.Action == "Failed Login") ? "Failed" : "Success",
                    IpAddress = l.IpAddress
                })
                .ToListAsync();

            var vm = new AuthenticationLogViewModel
            {
                Search = search,
                Role = role,
                Roles = allRoles,
                Logs = logs,
                TotalLogs = total,
                Page = page,
                PageSize = pageSize
            };

            return View(vm);
        }

        //===================Inventory===================
        public IActionResult DentalSupplies()
        {
            return RedirectToAction("DentalSupplies", "Inventory");
        }
        
        public IActionResult Stocks()
        {
            return RedirectToAction("Stocks", "Inventory");
        }
        //===================Inventory===================
        public async Task<IActionResult> Records(
            string preset = "month",
            DateTime? from = null,
            DateTime? to = null,
            int invoicePage = 1)
        {
            // ── Date range resolution ────────────────────────────────────────
            var now = DateTime.UtcNow;
            DateTime dateFrom, dateTo;
            switch (preset)
            {
                case "today":
                    dateFrom = now.Date;
                    dateTo   = now.Date.AddDays(1).AddTicks(-1);
                    break;
                case "week":
                    dateFrom = now.Date.AddDays(-(int)now.DayOfWeek);
                    dateTo   = dateFrom.AddDays(7).AddTicks(-1);
                    break;
                case "custom" when from.HasValue && to.HasValue:
                    dateFrom = from.Value.Date;
                    dateTo   = to.Value.Date.AddDays(1).AddTicks(-1);
                    break;
                default: // month
                    preset   = "month";
                    dateFrom = new DateTime(now.Year, now.Month, 1);
                    dateTo   = dateFrom.AddMonths(1).AddTicks(-1);
                    break;
            }

            // Previous period for delta calculation
            var span       = dateTo - dateFrom;
            var prevFrom   = dateFrom - span - TimeSpan.FromTicks(1);
            var prevTo     = dateFrom - TimeSpan.FromTicks(1);

            // ── KPIs ─────────────────────────────────────────────────────────
            var payments = await _db.Set<DentalSync.Models.Payment>()
                .Where(p => p.PaymentDate >= dateFrom && p.PaymentDate <= dateTo)
                .SumAsync(p => (decimal?)p.Amount) ?? 0m;

            var prevPayments = await _db.Set<DentalSync.Models.Payment>()
                .Where(p => p.PaymentDate >= prevFrom && p.PaymentDate <= prevTo)
                .SumAsync(p => (decimal?)p.Amount) ?? 0m;

            var apptCount = await _db.Set<DentalSync.Models.Appointment>()
                .CountAsync(a => a.CreatedAt >= dateFrom && a.CreatedAt <= dateTo);

            var prevApptCount = await _db.Set<DentalSync.Models.Appointment>()
                .CountAsync(a => a.CreatedAt >= prevFrom && a.CreatedAt <= prevTo);

            var newPatients = await _db.Set<DentalSync.Models.Patient>()
                .CountAsync(p => p.CreatedAt >= dateFrom && p.CreatedAt <= dateTo);

            var prevNewPatients = await _db.Set<DentalSync.Models.Patient>()
                .CountAsync(p => p.CreatedAt >= prevFrom && p.CreatedAt <= prevTo);

            var completedTreatments = await _db.Set<DentalSync.Models.TreatmentRecord>()
                .CountAsync(t => t.TreatmentDate >= dateFrom && t.TreatmentDate <= dateTo);

            var prevTreatments = await _db.Set<DentalSync.Models.TreatmentRecord>()
                .CountAsync(t => t.TreatmentDate >= prevFrom && t.TreatmentDate <= prevTo);

            var outstanding = await _db.Set<DentalSync.Models.Invoice>()
                .Where(i => i.Status == "Pending" || i.Status == "PartiallyPaid")
                .SumAsync(i => (decimal?)i.TotalAmount) ?? 0m;

            var paidAmt = await _db.Set<DentalSync.Models.Payment>()
                .Where(p => p.Invoice.Status == "Pending" || p.Invoice.Status == "PartiallyPaid")
                .SumAsync(p => (decimal?)p.Amount) ?? 0m;

            outstanding = Math.Max(0, outstanding - paidAmt);

            var today30 = DateOnly.FromDateTime(now.AddDays(30));
            var todayDo = DateOnly.FromDateTime(now);
            var invAlerts = await _db.Set<DentalSync.Models.Supply>()
                .CountAsync(s => s.Quantity == 0 ||
                                 (s.MinimumStock.HasValue && s.Quantity <= s.MinimumStock) ||
                                 (s.ExpirationDate.HasValue && s.ExpirationDate <= today30 && s.ExpirationDate >= todayDo));

            // ── Financial stats ───────────────────────────────────────────────
            var paidTotal = await _db.Set<DentalSync.Models.Invoice>()
                .Where(i => i.Status == "Paid" && i.InvoiceDate >= dateFrom && i.InvoiceDate <= dateTo)
                .SumAsync(i => (decimal?)i.TotalAmount) ?? 0m;

            var unpaidTotal = await _db.Set<DentalSync.Models.Invoice>()
                .Where(i => (i.Status == "Pending" || i.Status == "Cancelled") && i.InvoiceDate >= dateFrom && i.InvoiceDate <= dateTo)
                .SumAsync(i => (decimal?)i.TotalAmount) ?? 0m;

            var partialTotal = await _db.Set<DentalSync.Models.Invoice>()
                .Where(i => i.Status == "PartiallyPaid" && i.InvoiceDate >= dateFrom && i.InvoiceDate <= dateTo)
                .SumAsync(i => (decimal?)i.TotalAmount) ?? 0m;

            var paymentMethods = await _db.Set<DentalSync.Models.Payment>()
                .Where(p => p.PaymentDate >= dateFrom && p.PaymentDate <= dateTo)
                .GroupBy(p => p.PaymentMethod)
                .Select(g => new DentalSync.ViewModels.ChartPoint
                {
                    Label = g.Key,
                    Value = g.Sum(p => p.Amount),
                    Count = g.Count()
                })
                .ToListAsync();

            // ── Appointment status stats ──────────────────────────────────────
            var apptStatuses = await _db.Set<DentalSync.Models.Appointment>()
                .Where(a => a.CreatedAt >= dateFrom && a.CreatedAt <= dateTo)
                .GroupBy(a => a.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync();

            var apptStats = new DentalSync.ViewModels.AppointmentStatsViewModel
            {
                Scheduled  = apptStatuses.FirstOrDefault(s => s.Status == "Scheduled")?.Count ?? 0,
                Confirmed  = apptStatuses.FirstOrDefault(s => s.Status == "Confirmed")?.Count ?? 0,
                Completed  = apptStatuses.FirstOrDefault(s => s.Status == "Completed")?.Count ?? 0,
                Cancelled  = apptStatuses.FirstOrDefault(s => s.Status == "Cancelled")?.Count ?? 0,
                NoShow     = apptStatuses.FirstOrDefault(s => s.Status == "NoShow")?.Count ?? 0,
            };

            // ── Inventory stats ───────────────────────────────────────────────
            var totalSupplies = await _db.Set<DentalSync.Models.Supply>().CountAsync();
            var lowStock      = await _db.Set<DentalSync.Models.Supply>().CountAsync(s => s.MinimumStock.HasValue && s.Quantity > 0 && s.Quantity <= s.MinimumStock);
            var outOfStock    = await _db.Set<DentalSync.Models.Supply>().CountAsync(s => s.Quantity == 0);
            var expiring      = await _db.Set<DentalSync.Models.Supply>().CountAsync(s => s.ExpirationDate.HasValue && s.ExpirationDate <= today30 && s.ExpirationDate >= todayDo);

            // ── Revenue by month (last 6 months) ─────────────────────────────
            var sixMonthsAgo = new DateTime(now.Year, now.Month, 1).AddMonths(-5);
            var revenueByMonth = (await _db.Set<DentalSync.Models.Payment>()
                .Where(p => p.PaymentDate >= sixMonthsAgo)
                .GroupBy(p => new { p.PaymentDate.Year, p.PaymentDate.Month })
                .Select(g => new
                {
                    g.Key.Year,
                    g.Key.Month,
                    Value = g.Sum(p => p.Amount),
                    Count = g.Count()
                })
                .ToListAsync())
                .Select(g => new DentalSync.ViewModels.ChartPoint
                {
                    Label = $"{g.Year}-{g.Month:D2}",
                    Value = g.Value,
                    Count = g.Count
                })
                .OrderBy(c => c.Label)
                .ToList();

            // ── Patient growth (last 6 months) ────────────────────────────────
            var patientGrowth = (await _db.Set<DentalSync.Models.Patient>()
                .Where(p => p.CreatedAt >= sixMonthsAgo)
                .GroupBy(p => new { p.CreatedAt.Year, p.CreatedAt.Month })
                .Select(g => new
                {
                    g.Key.Year,
                    g.Key.Month,
                    Count = g.Count()
                })
                .ToListAsync())
                .Select(g => new DentalSync.ViewModels.ChartPoint
                {
                    Label = $"{g.Year}-{g.Month:D2}",
                    Count = g.Count
                })
                .OrderBy(c => c.Label)
                .ToList();

            // ── Top services ──────────────────────────────────────────────────
            var topServices = await _db.Set<DentalSync.Models.TreatmentRecord>()
                .Where(t => t.TreatmentDate >= dateFrom && t.TreatmentDate <= dateTo)
                .GroupBy(t => t.Service.Name)
                .Select(g => new DentalSync.ViewModels.TopServiceRow
                {
                    ServiceName = g.Key,
                    Count       = g.Count(),
                    Revenue     = g.Sum(t => t.Service.Cost)
                })
                .OrderByDescending(r => r.Count)
                .Take(6)
                .ToListAsync();

            // ── Recent invoices ───────────────────────────────────────────────
            const int invPageSize = 8;
            var invQuery = _db.Set<DentalSync.Models.Invoice>()
                .Where(i => i.InvoiceDate >= dateFrom && i.InvoiceDate <= dateTo)
                .OrderByDescending(i => i.InvoiceDate);

            var totalInv = await invQuery.CountAsync();
            var invoices = await invQuery
                .Skip((invoicePage - 1) * invPageSize)
                .Take(invPageSize)
                .Select(i => new DentalSync.ViewModels.RecentInvoiceRow
                {
                    InvoiceId     = i.Id,
                    InvoiceNumber = i.InvoiceNumber,
                    PatientName   = i.Patient.FirstName + " " + i.Patient.LastName,
                    InvoiceDate   = i.InvoiceDate,
                    TotalAmount   = i.TotalAmount,
                    PaidAmount    = i.Payments.Sum(p => p.Amount),
                    Status        = i.Status
                })
                .ToListAsync();

            // ── Assemble VM ───────────────────────────────────────────────────
            var activeTimeLimit = DateTime.Now.AddMinutes(-30);
            var activeSessions = await _db.AuditLogs
                .AsNoTracking()
                .Where(l => l.Module == "Authentication" && l.Action == "Login" && l.DateTime >= activeTimeLimit)
                .Select(l => l.User)
                .Distinct()
                .CountAsync();

            var failedLogins = await _db.AuditLogs
                .AsNoTracking()
                .CountAsync(l => l.Module == "Authentication" && l.Action == "Failed Login");

            var lockedAccounts = await userManager.Users
                .CountAsync(u => u.LockoutEnd.HasValue && u.LockoutEnd.Value > DateTimeOffset.UtcNow);

            var loginsByMonth = (await _db.AuditLogs
                .AsNoTracking()
                .Where(l => l.Module == "Authentication" && l.Action == "Login" && l.DateTime >= sixMonthsAgo)
                .GroupBy(l => new { l.DateTime.Year, l.DateTime.Month })
                .Select(g => new
                {
                    g.Key.Year,
                    g.Key.Month,
                    Count = g.Count()
                })
                .ToListAsync())
                .Select(g => new DentalSync.ViewModels.ChartPoint
                {
                    Label = $"{g.Year}-{g.Month:D2}",
                    Count = g.Count
                })
                .OrderBy(c => c.Label)
                .ToList();

            var failedLoginsByMonth = (await _db.AuditLogs
                .AsNoTracking()
                .Where(l => l.Module == "Authentication" && l.Action == "Failed Login" && l.DateTime >= sixMonthsAgo)
                .GroupBy(l => new { l.DateTime.Year, l.DateTime.Month })
                .Select(g => new
                {
                    g.Key.Year,
                    g.Key.Month,
                    Count = g.Count()
                })
                .ToListAsync())
                .Select(g => new DentalSync.ViewModels.ChartPoint
                {
                    Label = $"{g.Year}-{g.Month:D2}",
                    Count = g.Count
                })
                .OrderBy(c => c.Label)
                .ToList();

            // ── Browser usage (all-time) ──────────────────────────────────────
            var browserStats = (await _db.AuditLogs
                .AsNoTracking()
                .Where(l => !string.IsNullOrEmpty(l.Browser))
                .GroupBy(l => l.Browser)
                .Select(g => new { Browser = g.Key, Count = g.Count() })
                .ToListAsync())
                .Select(g => new DentalSync.ViewModels.ChartPoint
                {
                    Label = g.Browser,
                    Count = g.Count
                })
                .OrderByDescending(c => c.Count)
                .ToList();

            static double? Delta(decimal curr, decimal prev) =>
                prev == 0 ? null : Math.Round((double)((curr - prev) / prev * 100), 1);

            static double? DeltaI(int curr, int prev) =>
                prev == 0 ? null : Math.Round((double)((curr - prev) / (double)prev * 100), 1);

            var vm = new DentalSync.ViewModels.ReportsViewModel
            {
                Preset   = preset,
                DateFrom = dateFrom,
                DateTo   = dateTo,
                Kpi = new DentalSync.ViewModels.ReportsKpiViewModel
                {
                    TotalRevenue         = payments,
                    TotalAppointments    = apptCount,
                    NewPatients          = newPatients,
                    CompletedTreatments  = completedTreatments,
                    OutstandingPayments  = outstanding,
                    InventoryAlerts      = invAlerts,
                    RevenueDelta         = Delta(payments, prevPayments),
                    AppointmentsDelta    = DeltaI(apptCount, prevApptCount),
                    PatientsDelta        = DeltaI(newPatients, prevNewPatients),
                    TreatmentsDelta      = DeltaI(completedTreatments, prevTreatments),
                },
                Financial = new DentalSync.ViewModels.FinancialStatsViewModel
                {
                    TotalRevenue   = payments,
                    PaidInvoices   = paidTotal,
                    UnpaidInvoices = unpaidTotal,
                    PartiallyPaid  = partialTotal,
                    PaymentMethods = paymentMethods
                },
                AppointmentStats = apptStats,
                InventoryStats = new DentalSync.ViewModels.InventoryStatsViewModel
                {
                    TotalSupplies    = totalSupplies,
                    LowStock         = lowStock,
                    OutOfStock       = outOfStock,
                    ExpiringIn30Days = expiring
                },
                RevenueByMonth = revenueByMonth,
                PatientGrowth  = patientGrowth,
                TopServices    = topServices,
                RecentInvoices = invoices,
                InvoicePage    = invoicePage,
                InvoicePageSize = invPageSize,
                TotalInvoices  = totalInv,
                ActiveSessions = activeSessions,
                FailedLogins = failedLogins,
                LockedAccounts = lockedAccounts,
                LoginsByMonth = loginsByMonth,
                FailedLoginsByMonth = failedLoginsByMonth,
                BrowserStats = browserStats
            };

            return View(vm);
        }





        [Authorize]
        public async Task<IActionResult> Dashboard()
        {
            if (User.IsInRole("Patient"))
            {
                return RedirectToAction("Dashboard", "Patient");
            }
            if (User.IsInRole("Dentist"))
            {
                return RedirectToAction("Dashboard", "Dentist");
            }

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
