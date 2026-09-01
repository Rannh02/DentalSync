using DentalSync.Data;
using DentalSync.Models;
using DentalSync.Services;
using DentalSync.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DentalSync.Controllers
{
    [Authorize(Roles = "Administrator,Dentist")]
    public class ServicesController : Controller
    {
        private readonly AppDbContext _context;
        private readonly AuditService _audit;

        public ServicesController(AppDbContext context, AuditService audit)
        {
            _context = context;
            _audit = audit;
        }

        public async Task<IActionResult> Index(string search = "", string status = "", int page = 1)
        {
            const int pageSize = 5;
            var query = _context.Services.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(s =>
                    EF.Functions.Like(s.Name, $"%{term}%") ||
                    (s.Description != null && EF.Functions.Like(s.Description, $"%{term}%")));
            }

            if (status.Equals("active", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(s => s.IsActive);
            }
            else if (status.Equals("inactive", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(s => !s.IsActive);
            }

            var totalServices = await query.CountAsync();
            var currentPage = Math.Max(1, page);

            var services = await query
                .OrderByDescending(s => s.CreatedAt)
                .Skip((currentPage - 1) * pageSize)
                .Take(pageSize)
                .Select(s => new ServiceListItemViewModel
                {
                    Id = s.Id,
                    Name = s.Name,
                    Description = s.Description,
                    Cost = s.Cost,
                    EstimatedDurationMinutes = s.EstimatedDurationMinutes,
                    IsActive = s.IsActive,
                    CreatedAt = s.CreatedAt
                })
                .ToListAsync();

            var model = new ServiceManagementViewModel
            {
                Search = search,
                Status = status,
                Page = currentPage,
                PageSize = pageSize,
                TotalServices = totalServices,
                Services = services
            };

            return View("~/Views/Home/Services.cshtml", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateServiceViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["ServiceError"] = string.Join(" ", ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage));
                return RedirectToAction(nameof(Index));
            }

            var service = new DentalService
            {
                Name = model.Name.Trim(),
                Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim(),
                Cost = model.Cost,
                EstimatedDurationMinutes = model.EstimatedDurationMinutes,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.Services.Add(service);
            await _context.SaveChangesAsync();

            await _audit.LogAsync("Create Service", "Services", $"Added dental service '{service.Name}' (₱{service.Cost:N2})");

            TempData["ServiceSuccess"] = "Dental Service created successfully!";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(EditServiceViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["ServiceError"] = string.Join(" ", ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage));
                return RedirectToAction(nameof(Index));
            }

            var service = await _context.Services.FindAsync(model.Id);
            if (service == null)
            {
                return NotFound();
            }

            service.Name = model.Name.Trim();
            service.Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim();
            service.Cost = model.Cost;
            service.EstimatedDurationMinutes = model.EstimatedDurationMinutes;

            await _context.SaveChangesAsync();

            await _audit.LogAsync("Edit Service", "Services", $"Updated dental service '{service.Name}'");

            TempData["ServiceSuccess"] = "Service updated successfully!";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            var service = await _context.Services.FindAsync(id);
            if (service == null)
            {
                return NotFound();
            }

            service.IsActive = !service.IsActive;
            await _context.SaveChangesAsync();

            await _audit.LogAsync("Toggle Service Status", "Services", $"Toggled status of service '{service.Name}' to {(service.IsActive ? "Active" : "Inactive")}");

            TempData["ServiceSuccess"] = $"Service '{service.Name}' updated to {(service.IsActive ? "Active" : "Inactive")}.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var service = await _context.Services.FindAsync(id);
            if (service == null)
            {
                return NotFound();
            }

            _context.Services.Remove(service);
            await _context.SaveChangesAsync();

            await _audit.LogAsync("Delete Service", "Services", $"Deleted dental service '{service.Name}'");

            TempData["ServiceSuccess"] = $"Service '{service.Name}' deleted successfully.";
            return RedirectToAction(nameof(Index));
        }
    }
}
