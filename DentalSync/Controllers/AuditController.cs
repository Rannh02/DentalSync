using DentalSync.Data;
using DentalSync.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DentalSync.Controllers
{
    [Authorize]
    public class AuditController : Controller
    {
        private readonly AppDbContext _db;

        public AuditController(AppDbContext db)
        {
            _db = db;
        }

        public async Task<IActionResult> Audit(string search = "", string role = "", int page = 1)
        {
            var allRoles = new List<string> { "Administrator", "Receptionist", "Dentist", "Patient" };

            // Never expose Superadmin activity to Administrators
            var query = _db.AuditLogs.AsNoTracking()
                .Where(l => l.Role != "Superadmin")
                .AsQueryable();

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
            var logs = await query
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

            return View("~/Views/Home/Audit.cshtml", vm);
        }

        public async Task<IActionResult> Authentication(string search = "", string role = "", int page = 1)
        {
            var allRoles = new List<string> { "Administrator", "Receptionist", "Dentist", "Patient" };

            // Never expose Superadmin activity to Administrators
            var query = _db.AuditLogs.AsNoTracking()
                .Where(l => l.Module == "Authentication" && l.Role != "Superadmin");

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

            return View("~/Views/Home/Authentication.cshtml", vm);
        }
    }
}
