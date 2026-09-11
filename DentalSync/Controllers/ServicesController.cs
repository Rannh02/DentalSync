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
        private readonly InventoryDeductionService _deductionService;

        public ServicesController(AppDbContext context, AuditService audit, InventoryDeductionService deductionService)
        {
            _context = context;
            _audit = audit;
            _deductionService = deductionService;
        }

        public async Task<IActionResult> Index(string search = "", string status = "", int page = 1)
        {
            await SeedPredefinedServicesIfEmptyAsync();

            const int pageSize = 8;
            var query = _context.Services.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(s =>
                    EF.Functions.Like(s.Name, $"%{term}%") ||
                    (s.Category != null && EF.Functions.Like(s.Category, $"%{term}%")) ||
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
                .OrderBy(s => s.Category)
                .ThenBy(s => s.Name)
                .Skip((currentPage - 1) * pageSize)
                .Take(pageSize)
                .Select(s => new ServiceListItemViewModel
                {
                    Id = s.Id,
                    Name = s.Name,
                    Category = s.Category ?? "General",
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
                Category = string.IsNullOrWhiteSpace(model.Category) ? "General" : model.Category.Trim(),
                Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim(),
                Cost = model.Cost,
                EstimatedDurationMinutes = model.EstimatedDurationMinutes,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.Services.Add(service);
            await _context.SaveChangesAsync();

            // Deduct stock for the service category automatically if supplies exist
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            await _deductionService.DeductSupplyForCategoryAsync(service.Category, service.Name, userId);

            await _audit.LogAsync("Create Service", "Services", $"Added dental service '{service.Name}' in category '{service.Category}' (₱{service.Cost:N2})");

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
            service.Category = string.IsNullOrWhiteSpace(model.Category) ? "General" : model.Category.Trim();
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

        private async Task SeedPredefinedServicesIfEmptyAsync()
        {
            if (await _context.Services.AnyAsync()) return;

            var predefined = new List<DentalService>
            {
                // Preventive
                new() { Category = "Preventive", Name = "Dental Checkup", Cost = 500, EstimatedDurationMinutes = 30, Description = "Comprehensive oral examination and consultation." },
                new() { Category = "Preventive", Name = "Teeth Cleaning / Scaling", Cost = 800, EstimatedDurationMinutes = 45, Description = "Removal of plaque, tartar, and surface stains." },
                new() { Category = "Preventive", Name = "Fluoride Treatment", Cost = 500, EstimatedDurationMinutes = 20, Description = "Topical fluoride application to strengthen enamel." },
                new() { Category = "Preventive", Name = "Dental Sealants", Cost = 700, EstimatedDurationMinutes = 30, Description = "Protective resin coating for molars and premolars." },
                new() { Category = "Preventive", Name = "Oral Cancer Screening", Cost = 500, EstimatedDurationMinutes = 20, Description = "Visual and tactile examination for oral tissue anomalies." },
                new() { Category = "Preventive", Name = "Dental X-Ray", Cost = 500, EstimatedDurationMinutes = 15, Description = "Periapical or panoramic digital radiograph." },

                // Restorative
                new() { Category = "Restorative", Name = "Tooth-Colored Filling", Cost = 1000, EstimatedDurationMinutes = 45, Description = "Composite resin dental restoration for cavities." },
                new() { Category = "Restorative", Name = "Dental Crown", Cost = 8000, EstimatedDurationMinutes = 60, Description = "Full coverage porcelain or ceramic tooth cap." },
                new() { Category = "Restorative", Name = "Dental Bridge", Cost = 15000, EstimatedDurationMinutes = 90, Description = "Fixed multi-unit restoration for missing teeth." },
                new() { Category = "Restorative", Name = "Full Dentures", Cost = 15000, EstimatedDurationMinutes = 90, Description = "Complete removable prosthetic arch." },
                new() { Category = "Restorative", Name = "Partial Dentures", Cost = 10000, EstimatedDurationMinutes = 60, Description = "Removable acrylic or metal frame prosthetic." },
                new() { Category = "Restorative", Name = "Root Canal Treatment", Cost = 8000, EstimatedDurationMinutes = 90, Description = "Endodontic therapy to remove infected pulp tissue." },
                new() { Category = "Restorative", Name = "Inlay / Onlay", Cost = 6000, EstimatedDurationMinutes = 60, Description = "Custom indirect ceramic restoration." },

                // Cosmetic
                new() { Category = "Cosmetic", Name = "Teeth Whitening", Cost = 8000, EstimatedDurationMinutes = 60, Description = "In-office professional LED laser bleaching." },
                new() { Category = "Cosmetic", Name = "Dental Veneers", Cost = 10000, EstimatedDurationMinutes = 60, Description = "Custom porcelain shell for anterior teeth aesthetic." },
                new() { Category = "Cosmetic", Name = "Cosmetic Bonding", Cost = 2500, EstimatedDurationMinutes = 45, Description = "Composite resin reshaping for discolored teeth." },
                new() { Category = "Cosmetic", Name = "Tooth Reshaping", Cost = 2000, EstimatedDurationMinutes = 30, Description = "Enameloplasty for minor tooth contour modification." },
                new() { Category = "Cosmetic", Name = "Smile Makeover", Cost = 30000, EstimatedDurationMinutes = 120, Description = "Comprehensive multi-procedure aesthetic smile design." },

                // Orthodontics
                new() { Category = "Orthodontics", Name = "Traditional Braces", Cost = 50000, EstimatedDurationMinutes = 60, Description = "Full metal bracket realignment system." },
                new() { Category = "Orthodontics", Name = "Ceramic Braces", Cost = 60000, EstimatedDurationMinutes = 60, Description = "Tooth-colored aesthetic bracket alignment system." },
                new() { Category = "Orthodontics", Name = "Clear Aligners", Cost = 80000, EstimatedDurationMinutes = 45, Description = "Custom removable transparent alignment trays." },
                new() { Category = "Orthodontics", Name = "Retainers", Cost = 5000, EstimatedDurationMinutes = 30, Description = "Post-treatment retention appliance." },

                // Oral Surgery
                new() { Category = "Oral Surgery", Name = "Simple Tooth Extraction", Cost = 1500, EstimatedDurationMinutes = 30, Description = "Non-surgical tooth removal." },
                new() { Category = "Oral Surgery", Name = "Wisdom Tooth Extraction", Cost = 5000, EstimatedDurationMinutes = 60, Description = "Surgical extraction of impacted third molar." },
                new() { Category = "Oral Surgery", Name = "Dental Implant", Cost = 60000, EstimatedDurationMinutes = 90, Description = "Surgical placement of titanium implant post." },
                new() { Category = "Oral Surgery", Name = "Bone Grafting", Cost = 15000, EstimatedDurationMinutes = 60, Description = "Ridge augmentation and bone graft placement." },

                // Gum Care
                new() { Category = "Gum Care", Name = "Deep Cleaning", Cost = 2500, EstimatedDurationMinutes = 60, Description = "Subgingival scaling and root planing." },
                new() { Category = "Gum Care", Name = "Scaling & Root Planing", Cost = 3000, EstimatedDurationMinutes = 60, Description = "Periodontal therapeutic cleaning." },
                new() { Category = "Gum Care", Name = "Gum Surgery", Cost = 10000, EstimatedDurationMinutes = 90, Description = "Flap surgery or gingivectomy procedure." },
                new() { Category = "Gum Care", Name = "Gum Grafting", Cost = 15000, EstimatedDurationMinutes = 90, Description = "Soft tissue graft for severe recession." },

                // Pediatric
                new() { Category = "Pediatric", Name = "Children's Dental Checkup", Cost = 400, EstimatedDurationMinutes = 30, Description = "Pediatric oral health examination." },
                new() { Category = "Pediatric", Name = "Children's Teeth Cleaning", Cost = 600, EstimatedDurationMinutes = 30, Description = "Gentle pediatric prophy cleaning." },
                new() { Category = "Pediatric", Name = "Children's Filling", Cost = 800, EstimatedDurationMinutes = 30, Description = "Primary tooth glass ionomer or composite filling." },
                new() { Category = "Pediatric", Name = "Space Maintainer", Cost = 3000, EstimatedDurationMinutes = 45, Description = "Pediatric arch space maintenance appliance." },

                // Emergency
                new() { Category = "Emergency", Name = "Emergency Consultation", Cost = 500, EstimatedDurationMinutes = 30, Description = "Urgent diagnostic assessment and pain relief." },
                new() { Category = "Emergency", Name = "Toothache Treatment", Cost = 1000, EstimatedDurationMinutes = 45, Description = "Palliative emergency treatment for acute toothache." },
                new() { Category = "Emergency", Name = "Broken/Chipped Tooth Treatment", Cost = 2000, EstimatedDurationMinutes = 45, Description = "Immediate stabilization for traumatic tooth fracture." },
                new() { Category = "Emergency", Name = "Abscess Treatment", Cost = 2500, EstimatedDurationMinutes = 45, Description = "Incision, drainage, and emergency therapy." },
                new() { Category = "Emergency", Name = "Lost Filling/Crown Treatment", Cost = 1500, EstimatedDurationMinutes = 30, Description = "Re-cementation or temporary restoration." },

                // Specialized
                new() { Category = "Specialized", Name = "TMJ Treatment", Cost = 5000, EstimatedDurationMinutes = 45, Description = "Temporomandibular joint evaluation and therapy." },
                new() { Category = "Specialized", Name = "Night Guard", Cost = 3500, EstimatedDurationMinutes = 30, Description = "Custom appliance for bruxism and teeth grinding." },
                new() { Category = "Specialized", Name = "Sleep Apnea Appliance", Cost = 15000, EstimatedDurationMinutes = 60, Description = "Mandibular repositioning sleep apnea appliance." }
            };

            _context.Services.AddRange(predefined);
            await _context.SaveChangesAsync();
        }
    }
}
