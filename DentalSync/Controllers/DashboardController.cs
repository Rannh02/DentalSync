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
    public class DashboardController : Controller
    {
        private readonly UserManager<Users> _userManager;
        private readonly AppDbContext _db;

        public DashboardController(UserManager<Users> userManager, AppDbContext db)
        {
            _userManager = userManager;
            _db = db;
        }

        [HttpGet]
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

            var user = await _userManager.GetUserAsync(User);
            ViewBag.UserFullName = user?.FullName ?? user?.UserName ?? "there";

            var today = DateOnly.FromDateTime(DateTime.Today);

            // KPIs
            var totalPatients = await _db.Patients.CountAsync();
            var todayApptsCount = await _db.Appointments.CountAsync(a => a.AppointmentDate == today);
            var totalApptsCount = await _db.Appointments.CountAsync();
            var totalRevenue = await _db.Payments.SumAsync(p => (decimal?)p.Amount) ?? 0m;

            // Pending Payments: open invoices TotalAmount minus payments made against them
            var openInvoices = await _db.Invoices
                .Where(i => i.Status == "Pending" || i.Status == "PartiallyPaid")
                .Include(i => i.Payments)
                .ToListAsync();

            decimal pendingPayments = 0m;
            foreach (var inv in openInvoices)
            {
                var paid = inv.Payments.Sum(p => p.Amount);
                var remaining = Math.Max(0, inv.TotalAmount - paid);
                pendingPayments += remaining;
            }

            // Today's Appointments (max 4 items)
            var todayApptsRaw = await _db.Appointments
                .Include(a => a.Patient)
                .Include(a => a.Dentist)
                .Include(a => a.Service)
                .Where(a => a.AppointmentDate == today)
                .OrderBy(a => a.StartTime)
                .Take(4)
                .ToListAsync();

            if (!todayApptsRaw.Any())
            {
                todayApptsRaw = await _db.Appointments
                    .Include(a => a.Patient)
                    .Include(a => a.Dentist)
                    .Include(a => a.Service)
                    .OrderByDescending(a => a.AppointmentDate)
                    .ThenBy(a => a.StartTime)
                    .Take(4)
                    .ToListAsync();
            }

            var todayAppts = todayApptsRaw.Select(a =>
            {
                var pName = $"{a.Patient?.FirstName} {a.Patient?.LastName}".Trim();
                if (string.IsNullOrWhiteSpace(pName)) pName = "Patient";

                var initials = GetInitials(pName);

                var dName = $"{a.Dentist?.FirstName} {a.Dentist?.LastName}".Trim();
                if (string.IsNullOrWhiteSpace(dName)) dName = "Staff";

                return new DashboardAppointmentItemViewModel
                {
                    Id = a.Id,
                    PatientName = pName,
                    Initials = initials,
                    ServiceName = a.Service?.Name ?? "General Checkup",
                    DentistName = dName.StartsWith("Dr.") ? dName : $"Dr. {dName}",
                    AppointmentDate = a.AppointmentDate,
                    StartTime = a.StartTime,
                    EndTime = a.EndTime,
                    CreatedAt = a.CreatedAt,
                    Status = a.Status
                };
            }).ToList();

            // Recent Activities from AuditLogs (max 4 items)
            var auditLogs = await _db.AuditLogs
                .OrderByDescending(l => l.DateTime)
                .Take(4)
                .ToListAsync();

            var recentActivities = auditLogs.Select(l => new DashboardActivityItemViewModel
            {
                Id = l.Id,
                User = l.User,
                Action = l.Action,
                Module = l.Module,
                Description = l.Description,
                Timestamp = l.DateTime,
                RelativeTimeText = GetRelativeTimeString(l.DateTime)
            }).ToList();

            var viewModel = new AdminDashboardViewModel
            {
                TotalPatients = totalPatients,
                TodayAppointmentsCount = todayApptsCount,
                TotalAppointmentsCount = totalApptsCount,
                TotalRevenue = totalRevenue,
                PendingPayments = pendingPayments,
                TodayAppointments = todayAppts,
                RecentActivities = recentActivities
            };

            return View("~/Views/Home/Dashboard.cshtml", viewModel);
        }

        private static string GetInitials(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "P";
            var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1) return parts[0][..1].ToUpper();
            return $"{parts[0][0]}{parts[^1][0]}".ToUpper();
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
    }
}
