using DentalSync.Data;
using DentalSync.Models;
using DentalSync.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DentalSync.Controllers
{
    [Authorize]
    public class RolePermissionsController : Controller
    {
        private readonly AppDbContext dbContext;

        private static readonly (string Key, string Name, string Description)[] Catalog =
        {
            ("dashboard.view", "Dashboard", "View the clinic dashboard"),
            ("users.manage", "User Management", "View, create, edit, and deactivate users"),
            ("roles.manage", "Roles & Permissions", "Manage role access settings"),
            ("patients.manage", "Patients", "View and manage patient records"),
            ("appointments.manage", "Appointments", "View and manage appointments"),
            ("services.manage", "Services", "View and manage clinic services"),
            ("billing.manage", "Billing & Payments", "View and manage bills and payments"),
            ("inventory.manage", "Inventory", "View and manage dental supplies and stocks"),
            ("reports.view", "Reports & Analytics", "View clinic reports and analytics"),
            ("audit.view", "Audit Logs", "View security and audit activity"),
            ("authentication.manage", "Authentication", "Manage authentication settings")
        };

        public RolePermissionsController(AppDbContext dbContext)
        {
            this.dbContext = dbContext;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            await EnsurePermissionCatalogAsync();
            return View("~/Views/Home/RolePerm.cshtml", await BuildViewModelAsync());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(IEnumerable<string>? enabledPermissions)
        {
            await EnsurePermissionCatalogAsync();
            var validPermissionIds = await dbContext.PermissionDefinitions.Select(permission => permission.Id).ToHashSetAsync();
            var enabled = (enabledPermissions ?? Array.Empty<string>())
                .Select(value => value.Split('|', 2))
                .Where(parts => parts.Length == 2 && UserRoles.All.Contains(parts[0]) && int.TryParse(parts[1], out var id) && validPermissionIds.Contains(id))
                .Select(parts => (Role: parts[0], PermissionId: int.Parse(parts[1])))
                .ToHashSet();

            var rolePermissions = await dbContext.RolePermissions.ToListAsync();
            foreach (var rolePermission in rolePermissions)
            {
                rolePermission.IsEnabled = enabled.Contains((rolePermission.RoleName, rolePermission.PermissionDefinitionId));
            }

            await dbContext.SaveChangesAsync();
            TempData["PermissionsSaved"] = "Permissions saved successfully.";
            return RedirectToAction(nameof(Index));
        }

        private async Task EnsurePermissionCatalogAsync()
        {
            var existing = await dbContext.PermissionDefinitions.ToDictionaryAsync(permission => permission.Key);
            foreach (var item in Catalog)
            {
                if (!existing.ContainsKey(item.Key))
                {
                    var permission = new PermissionDefinition
                    {
                        Key = item.Key,
                        Name = item.Name,
                        Description = item.Description,
                        SortOrder = Array.IndexOf(Catalog, item)
                    };
                    dbContext.PermissionDefinitions.Add(permission);
                    existing[item.Key] = permission;
                }
            }

            await dbContext.SaveChangesAsync();
            var definitions = existing.Values.ToList();
            var existingPairs = await dbContext.RolePermissions
                .Select(permission => new { permission.RoleName, permission.PermissionDefinitionId })
                .ToListAsync();

            foreach (var role in UserRoles.All)
            {
                foreach (var definition in definitions)
                {
                    if (existingPairs.Any(pair => pair.RoleName == role && pair.PermissionDefinitionId == definition.Id)) continue;
                    dbContext.RolePermissions.Add(new RolePermission
                    {
                        RoleName = role,
                        PermissionDefinitionId = definition.Id,
                        IsEnabled = DefaultAccess(role, definition.Key)
                    });
                }
            }

            await dbContext.SaveChangesAsync();
        }

        private async Task<RolePermissionViewModel> BuildViewModelAsync()
        {
            var definitions = await dbContext.PermissionDefinitions.OrderBy(permission => permission.SortOrder).ToListAsync();
            var access = await dbContext.RolePermissions.ToListAsync();
            return new RolePermissionViewModel
            {
                Permissions = definitions.Select(definition => new PermissionRowViewModel
                {
                    PermissionId = definition.Id,
                    Name = definition.Name,
                    Description = definition.Description,
                    RoleAccess = UserRoles.All.ToDictionary(
                        role => role,
                        role => access.Any(item => item.RoleName == role && item.PermissionDefinitionId == definition.Id && item.IsEnabled))
                }).ToList()
            };
        }

        private static bool DefaultAccess(string role, string permissionKey)
        {
            if (role == "Administrator") return true;
            if (role == "Receptionist") return permissionKey is "dashboard.view" or "patients.manage" or "appointments.manage" or "billing.manage";
            if (role == "Dentist") return permissionKey is "dashboard.view" or "patients.manage" or "appointments.manage" or "services.manage";
            return permissionKey is "dashboard.view";
        }
    }
}
