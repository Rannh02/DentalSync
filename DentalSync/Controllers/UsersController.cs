using DentalSync.Models;
using DentalSync.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DentalSync.Controllers
{
    [Authorize]
    public class UsersController : Controller
    {
        private readonly UserManager<Users> userManager;
        private readonly RoleManager<IdentityRole> roleManager;

        public UsersController(UserManager<Users> userManager, RoleManager<IdentityRole> roleManager)
        {
            this.userManager = userManager;
            this.roleManager = roleManager;
        }

        public async Task<IActionResult> Users(string search = "", string role = "", string status = "", int page = 1)
        {
            const int pageSize = 8;
            var users = await userManager.Users
                .OrderBy(user => user.FullName)
                .ThenBy(user => user.Email)
                .ToListAsync();

            var userRows = new List<UserListItemViewModel>();
            foreach (var user in users)
            {
                var roles = await userManager.GetRolesAsync(user);
                var lockoutActive = user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow;
                userRows.Add(new UserListItemViewModel
                {
                    Id = user.Id,
                    FullName = user.FullName ?? user.UserName ?? "Unnamed user",
                    Email = user.Email ?? string.Empty,
                    Role = roles.FirstOrDefault() ?? "Unassigned",
                    IsActive = !lockoutActive,
                    LockoutEnd = user.LockoutEnd?.DateTime
                });
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                userRows = userRows.Where(user =>
                    user.FullName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    user.Email.Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            if (!string.IsNullOrWhiteSpace(role) && UserRoles.All.Contains(role, StringComparer.OrdinalIgnoreCase))
            {
                userRows = userRows.Where(user => user.Role.Equals(role, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            if (status.Equals("active", StringComparison.OrdinalIgnoreCase))
            {
                userRows = userRows.Where(user => user.IsActive).ToList();
            }
            else if (status.Equals("inactive", StringComparison.OrdinalIgnoreCase))
            {
                userRows = userRows.Where(user => !user.IsActive).ToList();
            }

            var model = new UserManagementViewModel
            {
                Search = search,
                Role = role,
                Status = status,
                Page = Math.Max(1, page),
                PageSize = pageSize,
                TotalUsers = userRows.Count,
                Users = userRows.Skip((Math.Max(1, page) - 1) * pageSize).Take(pageSize).ToList()
            };

            if (model.Page > model.TotalPages)
            {
                model.Page = model.TotalPages;
                model.Users = userRows.Skip((model.Page - 1) * pageSize).Take(pageSize).ToList();
            }

            return View("~/Views/Home/Users.cshtml", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateUser(CreateUserViewModel model)
        {
            if (!UserRoles.Creatable.Contains(model.Role))
            {
                ModelState.AddModelError(nameof(model.Role), "Select a valid role.");
            }

            if (!ModelState.IsValid)
            {
                TempData["UserCreateError"] = string.Join(" ", ModelState.Values.SelectMany(value => value.Errors).Select(error => error.ErrorMessage));
                return RedirectToAction(nameof(Users));
            }

            var user = new Users
            {
                FullName = model.FullName,
                UserName = model.UserName,
                Email = model.Email,
                EmailConfirmed = true,
                LockoutEnabled = true
            };
            var createResult = await userManager.CreateAsync(user, model.Password);
            if (!createResult.Succeeded)
            {
                TempData["UserCreateError"] = string.Join(" ", createResult.Errors.Select(error => error.Description));
                return RedirectToAction(nameof(Users));
            }

            if (!await roleManager.RoleExistsAsync(model.Role))
            {
                await roleManager.CreateAsync(new IdentityRole(model.Role));
            }
            await userManager.AddToRoleAsync(user, model.Role);
            return RedirectToAction(nameof(Users));
        }

        public async Task<IActionResult> UserDetails(string id)
        {
            var user = await userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var roles = await userManager.GetRolesAsync(user);
            return View("UserManagement/UserDetails", new UserListItemViewModel
            {
                Id = user.Id,
                FullName = user.FullName ?? user.UserName ?? "Unnamed user",
                Email = user.Email ?? string.Empty,
                Role = roles.FirstOrDefault() ?? "Unassigned",
                IsActive = !user.LockoutEnd.HasValue || user.LockoutEnd <= DateTimeOffset.UtcNow,
                LockoutEnd = user.LockoutEnd?.DateTime
            });
        }

        [HttpGet]
        public async Task<IActionResult> EditUser(string id)
        {
            var user = await userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var roles = await userManager.GetRolesAsync(user);
            return View("UserManagement/EditUser", new EditUserViewModel
            {
                Id = user.Id,
                FullName = user.FullName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                Role = roles.FirstOrDefault() ?? "Patient"
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditUser(EditUserViewModel model)
        {
            if (!UserRoles.All.Contains(model.Role)) ModelState.AddModelError(nameof(model.Role), "Select a valid role.");
            var user = await userManager.FindByIdAsync(model.Id);
            if (user == null) return NotFound();
            if (!ModelState.IsValid) return View("UserManagement/EditUser", model);

            user.FullName = model.FullName;
            user.Email = model.Email;
            user.UserName = model.Email;
            var updateResult = await userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                foreach (var error in updateResult.Errors) ModelState.AddModelError(string.Empty, error.Description);
                return View("UserManagement/EditUser", model);
            }

            var currentRoles = await userManager.GetRolesAsync(user);
            if (currentRoles.Count > 0) await userManager.RemoveFromRolesAsync(user, currentRoles);
            if (!await roleManager.RoleExistsAsync(model.Role)) await roleManager.CreateAsync(new IdentityRole(model.Role));
            await userManager.AddToRoleAsync(user, model.Role);
            return RedirectToAction(nameof(Users));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleUserStatus(string id, string? returnUrl = null)
        {
            var user = await userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var isInactive = user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow;
            await userManager.SetLockoutEnabledAsync(user, true);
            await userManager.SetLockoutEndDateAsync(user, isInactive ? null : DateTimeOffset.UtcNow.AddYears(100));
            return RedirectToLocal(returnUrl);
        }

        [HttpGet]
        public async Task<IActionResult> ResetUserPassword(string id)
        {
            var user = await userManager.FindByIdAsync(id);
            if (user == null) return NotFound();
            return View("UserManagement/ResetUserPassword", new ResetUserPasswordViewModel { Id = user.Id, UserName = user.Email ?? user.UserName ?? string.Empty });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetUserPassword(ResetUserPasswordViewModel model)
        {
            var user = await userManager.FindByIdAsync(model.Id);
            if (user == null) return NotFound();
            if (!ModelState.IsValid) return View("UserManagement/ResetUserPassword", model);

            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            var result = await userManager.ResetPasswordAsync(user, token, model.NewPassword);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error.Description);
                return View("UserManagement/ResetUserPassword", model);
            }

            return RedirectToAction(nameof(Users));
        }

        private IActionResult RedirectToLocal(string? returnUrl)
        {
            return !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
                ? Redirect(returnUrl)
                : RedirectToAction(nameof(Users));
        }
    }
}
