using DentalSync.Data;
using DentalSync.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace DentalSync.Attributes
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
    public class HasPermissionAttribute : TypeFilterAttribute
    {
        public HasPermissionAttribute(string permissionKey) : base(typeof(HasPermissionFilter))
        {
            Arguments = new object[] { permissionKey };
        }
    }

    public class HasPermissionFilter : IAsyncActionFilter
    {
        private readonly string _permissionKey;
        private readonly AppDbContext _dbContext;
        private readonly UserManager<Users> _userManager;

        public HasPermissionFilter(string permissionKey, AppDbContext dbContext, UserManager<Users> userManager)
        {
            _permissionKey = permissionKey;
            _dbContext = dbContext;
            _userManager = userManager;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var user = await _userManager.GetUserAsync(context.HttpContext.User);
            if (user == null)
            {
                context.Result = new ChallengeResult();
                return;
            }

            var roles = await _userManager.GetRolesAsync(user);
            if (!roles.Any())
            {
                context.Result = new RedirectToActionResult("AccessDenied", "Account", null);
                return;
            }

            // Always allow Administrators access to Roles & Permissions page so they don't lock themselves out
            if (_permissionKey == "roles.manage" && roles.Any(r => r.Equals("Administrator", StringComparison.OrdinalIgnoreCase) || r.Equals("Admin", StringComparison.OrdinalIgnoreCase)))
            {
                await next();
                return;
            }

            // Check if any of user's assigned roles have the permission enabled in DB
            var hasAccess = await _dbContext.RolePermissions
                .AsNoTracking()
                .Include(rp => rp.PermissionDefinition)
                .AnyAsync(rp => roles.Contains(rp.RoleName) &&
                                rp.PermissionDefinition.Key == _permissionKey &&
                                rp.IsEnabled);

            if (!hasAccess)
            {
                context.Result = new RedirectToActionResult("AccessDenied", "Account", null);
                return;
            }

            await next();
        }
    }
}
