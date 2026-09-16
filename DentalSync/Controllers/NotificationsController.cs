using DentalSync.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DentalSync.Models;

namespace DentalSync.Controllers
{
    [Authorize]
    public class NotificationsController : Controller
    {
        private readonly AppDbContext _db;
        private readonly UserManager<Users> _userManager;

        public NotificationsController(AppDbContext db, UserManager<Users> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        [HttpGet]
        [Route("Home/GetNotifications")]
        [Route("Notifications/GetNotifications")]
        public async Task<IActionResult> GetNotifications()
        {
            var query = _db.AuditLogs.AsQueryable();
            if (User.IsInRole("Dentist"))
            {
                var user = await _userManager.GetUserAsync(User);
                var dentist = user == null
                    ? null
                    : await _db.Dentists.FirstOrDefaultAsync(d => d.UserId == user.Id);

                query = dentist == null
                    ? query.Where(l => false)
                    : query.Where(l => l.Action == "Request Patient Transfer" && l.Description.Contains($"[source-dentist:{dentist.Id}]"));
            }

            var logs = await query
                .OrderByDescending(l => l.DateTime)
                .Take(6)
                .ToListAsync();

            var notifs = logs.Select(l =>
            {
                var act = l.Action?.ToLower() ?? "";
                var mod = l.Module?.ToLower() ?? "";

                string category = "avatar-info";
                string icon = "i";

                if (act.Contains("login") || act.Contains("auth") || act.Contains("failed") || act.Contains("password"))
                {
                    category = "avatar-security";
                    icon = "🛡️";
                }
                else if (act.Contains("payment") || act.Contains("invoice") || mod.Contains("billing"))
                {
                    category = "avatar-billing";
                    icon = "₱";
                }
                else if (act.Contains("patient") || act.Contains("register") || mod.Contains("patient"))
                {
                    category = "avatar-patient";
                    icon = "+";
                }
                else if (act.Contains("appointment") || act.Contains("booking") || mod.Contains("appointment"))
                {
                    category = "avatar-appointment";
                    icon = "✓";
                }

                return new
                {
                    id = l.Id,
                    title = l.Action,
                    description = !string.IsNullOrWhiteSpace(l.Description) ? l.Description : $"{l.User} performed {l.Action}",
                    timeAgo = GetRelativeTimeString(l.DateTime),
                    category = category,
                    icon = icon
                };
            });

            return Json(notifs);
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
