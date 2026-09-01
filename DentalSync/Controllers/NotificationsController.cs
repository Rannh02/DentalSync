using DentalSync.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DentalSync.Controllers
{
    [Authorize]
    public class NotificationsController : Controller
    {
        private readonly AppDbContext _db;

        public NotificationsController(AppDbContext db)
        {
            _db = db;
        }

        [HttpGet]
        [Route("Home/GetNotifications")]
        [Route("Notifications/GetNotifications")]
        public async Task<IActionResult> GetNotifications()
        {
            var logs = await _db.AuditLogs
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
