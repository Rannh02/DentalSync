using DentalSync.Data;
using DentalSync.Models;
using Microsoft.AspNetCore.Identity;

namespace DentalSync.Services
{
    public class AuditService
    {
        private readonly AppDbContext _db;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly UserManager<Users> _userManager;

        public AuditService(AppDbContext db, IHttpContextAccessor httpContextAccessor, UserManager<Users> userManager)
        {
            _db = db;
            _httpContextAccessor = httpContextAccessor;
            _userManager = userManager;
        }

        /// <summary>
        /// Logs an action performed by the currently signed-in user.
        /// </summary>
        public async Task LogAsync(string action, string module, string description,
                                   string? overrideUser = null, string? overrideRole = null)
        {
            var ctx  = _httpContextAccessor.HttpContext;
            var ip   = ctx?.Connection?.RemoteIpAddress?.ToString() ?? "Unknown";
            var ua   = ctx?.Request?.Headers["User-Agent"].ToString() ?? "";
            var browser = ParseBrowser(ua);

            string userName = overrideUser ?? "Unknown";
            string roleName = overrideRole ?? "Unknown";

            if (overrideUser == null && ctx?.User?.Identity?.IsAuthenticated == true)
            {
                var user = await _userManager.GetUserAsync(ctx.User);
                if (user != null)
                {
                    userName = user.FullName ?? user.UserName ?? user.Email ?? "Unknown";
                    var roles = await _userManager.GetRolesAsync(user);
                    roleName = roles.FirstOrDefault() ?? "Unknown";
                }
            }

            _db.AuditLogs.Add(new AuditLog
            {
                DateTime    = DateTime.Now,
                User        = userName,
                Role        = roleName,
                Action      = action,
                Module      = module,
                Description = description,
                IpAddress   = ip == "::1" ? "127.0.0.1" : ip,
                Browser     = browser,
            });

            await _db.SaveChangesAsync();
        }

        /// <summary>
        /// Parses a User-Agent string into a friendly browser name.
        /// </summary>
        private static string ParseBrowser(string userAgent)
        {
            if (string.IsNullOrWhiteSpace(userAgent)) return "Unknown";

            // Order matters: Edge must be checked before Chrome (Edg token appears alongside Chrome)
            if (userAgent.Contains("Edg/") || userAgent.Contains("Edge/"))
                return "Microsoft Edge";
            if (userAgent.Contains("OPR/") || userAgent.Contains("Opera"))
                return "Opera";
            if (userAgent.Contains("Chrome/"))
                return "Chrome";
            if (userAgent.Contains("Firefox/"))
                return "Firefox";
            if (userAgent.Contains("Safari/") && !userAgent.Contains("Chrome"))
                return "Safari";
            if (userAgent.Contains("Trident/") || userAgent.Contains("MSIE"))
                return "Internet Explorer";

            return "Other";
        }
    }
}
