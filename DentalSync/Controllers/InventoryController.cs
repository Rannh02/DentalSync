using DentalSync.Data;
using DentalSync.Models;
using DentalSync.Services;
using DentalSync.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DentalSync.Controllers
{
    [Authorize]
    public class InventoryController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<Users> _userManager;
        private readonly AuditService _audit;

        public InventoryController(AppDbContext context, UserManager<Users> userManager, AuditService audit)
        {
            _context = context;
            _userManager = userManager;
            _audit = audit;
        }

        // =========================================================================
        // 1. Dental Supplies Page (Quick Inventory Monitoring & 4 KPIs)
        // =========================================================================
        [HttpGet]
        public async Task<IActionResult> DentalSupplies(
            string? search = "",
            int? categoryId = null,
            string? status = "",
            string? sort = "",
            int page = 1)
        {
            await SeedInitialInventoryIfEmptyAsync();

            var categories = await _context.InventoryCategories
                .OrderBy(c => c.CategoryName)
                .AsNoTracking()
                .ToListAsync();

            var allSupplies = await _context.Supplies
                .Include(s => s.Category)
                .AsNoTracking()
                .ToListAsync();

            var totalItems = allSupplies.Count;
            var inStockCount = allSupplies.Count(s => s.Status != "Archived" && s.Quantity > (s.MinimumStock ?? 0));
            var lowStockCount = allSupplies.Count(s => s.Status != "Archived" && s.Quantity > 0 && s.Quantity <= (s.MinimumStock ?? 0));
            var outOfStockCount = allSupplies.Count(s => s.Status != "Archived" && s.Quantity <= 0);

            var query = _context.Supplies
                .Include(s => s.Category)
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(s =>
                    EF.Functions.Like(s.SupplyName, $"%{term}%") ||
                    (s.Description != null && EF.Functions.Like(s.Description, $"%{term}%")));
            }

            if (categoryId.HasValue && categoryId.Value > 0)
            {
                query = query.Where(s => s.CategoryId == categoryId.Value);
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                var st = status.Trim().ToLower();
                if (st == "good")
                {
                    query = query.Where(s => s.Status != "Archived" && s.Quantity > (s.MinimumStock ?? 0));
                }
                else if (st == "low")
                {
                    query = query.Where(s => s.Status != "Archived" && s.Quantity > 0 && s.Quantity <= (s.MinimumStock ?? 0));
                }
                else if (st == "out")
                {
                    query = query.Where(s => s.Status != "Archived" && s.Quantity <= 0);
                }
                else if (st == "archived")
                {
                    query = query.Where(s => s.Status == "Archived");
                }
            }

            query = sort switch
            {
                "name_asc" => query.OrderBy(s => s.SupplyName),
                "name_desc" => query.OrderByDescending(s => s.SupplyName),
                "stock_asc" => query.OrderBy(s => s.Quantity),
                "stock_desc" => query.OrderByDescending(s => s.Quantity),
                _ => query.OrderBy(s => s.SupplyName)
            };

            const int pageSize = 5;
            var totalFiltered = await query.CountAsync();
            var currentPage = Math.Max(1, page);

            var rawSupplies = await query
                .Skip((currentPage - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var supplyList = rawSupplies.Select(s => new SupplyItemViewModel
            {
                Id = s.Id,
                SupplyName = s.SupplyName,
                CategoryId = s.CategoryId,
                CategoryName = s.Category?.CategoryName ?? "Uncategorized",
                Unit = s.Unit ?? "pcs",
                Quantity = s.Quantity,
                MinimumStock = s.MinimumStock ?? 0,
                PurchasePrice = s.PurchasePrice,
                ExpirationDate = s.ExpirationDate,
                RawStatus = s.Status,
                ComputedStatus = GetComputedStatus(s.Status, s.Quantity, s.MinimumStock ?? 0)
            }).ToList();

            var viewModel = new DentalSuppliesViewModel
            {
                TotalItems = totalItems,
                InStockCount = inStockCount,
                LowStockCount = lowStockCount,
                OutOfStockCount = outOfStockCount,
                Supplies = supplyList,
                Categories = categories,
                Search = search,
                CategoryId = categoryId,
                Status = status,
                Sort = sort,
                Page = currentPage,
                PageSize = pageSize,
                TotalCount = totalFiltered,
                TotalPages = (int)Math.Ceiling(totalFiltered / (double)pageSize)
            };

            return View(viewModel);
        }

        // =========================================================================
        // 2. Stocks Page (Audit & Transaction History)
        // =========================================================================
        [HttpGet]
        public async Task<IActionResult> Stocks(
            string? search = "",
            string? type = "",
            string? date = "",
            int? supplyId = null,
            int page = 1)
        {
            var supplies = await _context.Supplies
                .Where(s => s.Status != "Archived")
                .OrderBy(s => s.SupplyName)
                .AsNoTracking()
                .ToListAsync();

            var query = _context.StockTransactions
                .Include(st => st.Supply)
                    .ThenInclude(s => s.Category)
                .Include(st => st.User)
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(st =>
                    EF.Functions.Like(st.Supply.SupplyName, $"%{term}%") ||
                    (st.Reference != null && EF.Functions.Like(st.Reference, $"%{term}%")) ||
                    (st.Notes != null && EF.Functions.Like(st.Notes, $"%{term}%")));
            }

            if (!string.IsNullOrWhiteSpace(type) && (type.Equals("In", StringComparison.OrdinalIgnoreCase) || type.Equals("Out", StringComparison.OrdinalIgnoreCase)))
            {
                query = query.Where(st => st.TransactionType.ToLower() == type.Trim().ToLower());
            }

            if (supplyId.HasValue && supplyId.Value > 0)
            {
                query = query.Where(st => st.SupplyId == supplyId.Value);
            }

            if (!string.IsNullOrWhiteSpace(date) && DateTime.TryParse(date, out var parsedDate))
            {
                var start = parsedDate.Date;
                var end = start.AddDays(1).AddTicks(-1);
                query = query.Where(st => st.TransactionDate >= start && st.TransactionDate <= end);
            }

            const int pageSize = 5;
            var totalFiltered = await query.CountAsync();
            var currentPage = Math.Max(1, page);

            var list = await query
                .OrderByDescending(st => st.TransactionDate)
                .Skip((currentPage - 1) * pageSize)
                .Take(pageSize)
                .Select(st => new StockTransactionItemViewModel
                {
                    Id = st.Id,
                    Date = st.TransactionDate,
                    SupplyId = st.SupplyId,
                    SupplyName = st.Supply.SupplyName,
                    CategoryName = st.Supply.Category.CategoryName,
                    Unit = st.Supply.Unit ?? "pcs",
                    Type = st.TransactionType,
                    Quantity = st.Quantity,
                    UserName = st.User != null ? (st.User.FullName ?? st.User.UserName ?? "User") : "Administrator",
                    Reference = st.Reference,
                    Notes = st.Notes
                })
                .ToListAsync();

            var viewModel = new StockTransactionsViewModel
            {
                Transactions = list,
                Supplies = supplies,
                Search = search,
                Type = type,
                Date = date,
                SupplyId = supplyId,
                Page = currentPage,
                PageSize = pageSize,
                TotalCount = totalFiltered,
                TotalPages = (int)Math.Ceiling(totalFiltered / (double)pageSize)
            };

            return View(viewModel);
        }

        // =========================================================================
        // 3. Actions: Add, Edit, Stock In, Stock Out, Archive
        // =========================================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateSupply(CreateSupplyInputModel model)
        {
            if (string.IsNullOrWhiteSpace(model.SupplyName) || model.CategoryId <= 0)
            {
                TempData["InventoryError"] = "Please fill in the required supply name and category.";
                return RedirectToAction(nameof(DentalSupplies));
            }

            var supply = new Supply
            {
                SupplyName = model.SupplyName.Trim(),
                CategoryId = model.CategoryId,
                Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim(),
                Unit = string.IsNullOrWhiteSpace(model.Unit) ? "pcs" : model.Unit.Trim(),
                Quantity = Math.Max(0, model.InitialQuantity),
                MinimumStock = Math.Max(0, model.MinimumStock),
                PurchasePrice = model.PurchasePrice,
                ExpirationDate = model.ExpirationDate,
                Status = model.InitialQuantity <= 0 ? "Out of Stock" : (model.InitialQuantity <= model.MinimumStock ? "Low Stock" : "In Stock"),
                CreatedAt = DateTime.UtcNow
            };

            _context.Supplies.Add(supply);
            await _context.SaveChangesAsync();

            if (model.InitialQuantity > 0)
            {
                var currentUser = await _userManager.GetUserAsync(User);
                var transaction = new StockTransaction
                {
                    SupplyId = supply.Id,
                    UserId = currentUser?.Id,
                    TransactionType = "In",
                    Quantity = model.InitialQuantity,
                    TransactionDate = DateTime.UtcNow,
                    Reference = "INIT-STOCK",
                    Notes = "Initial inventory stock count"
                };
                _context.StockTransactions.Add(transaction);
                await _context.SaveChangesAsync();
            }

            await _audit.LogAsync("Create Supply", "Inventory", $"Added new supply '{supply.SupplyName}' ({supply.Quantity} {supply.Unit})");

            TempData["InventorySuccess"] = $"Supply '{supply.SupplyName}' added successfully!";
            return RedirectToAction(nameof(DentalSupplies));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditSupply(EditSupplyInputModel model)
        {
            var supply = await _context.Supplies.FindAsync(model.Id);
            if (supply == null) return NotFound();

            supply.SupplyName = model.SupplyName.Trim();
            supply.CategoryId = model.CategoryId;
            supply.Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim();
            supply.Unit = string.IsNullOrWhiteSpace(model.Unit) ? "pcs" : model.Unit.Trim();
            supply.MinimumStock = Math.Max(0, model.MinimumStock);
            supply.PurchasePrice = model.PurchasePrice;
            supply.ExpirationDate = model.ExpirationDate;
            supply.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            await _audit.LogAsync("Edit Supply", "Inventory", $"Updated supply '{supply.SupplyName}'");

            TempData["InventorySuccess"] = $"Supply '{supply.SupplyName}' updated successfully!";
            return RedirectToAction(nameof(DentalSupplies));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StockIn(StockMovementInputModel model)
        {
            if (model.Quantity <= 0)
            {
                TempData["InventoryError"] = "Quantity must be greater than zero.";
                return RedirectToAction(nameof(DentalSupplies));
            }

            var supply = await _context.Supplies.FindAsync(model.SupplyId);
            if (supply == null) return NotFound();

            supply.Quantity += model.Quantity;
            supply.UpdatedAt = DateTime.UtcNow;
            if (supply.Status != "Archived")
            {
                supply.Status = supply.Quantity <= (supply.MinimumStock ?? 0) ? "Low Stock" : "In Stock";
            }

            var currentUser = await _userManager.GetUserAsync(User);
            var transaction = new StockTransaction
            {
                SupplyId = supply.Id,
                UserId = currentUser?.Id,
                TransactionType = "In",
                Quantity = model.Quantity,
                TransactionDate = DateTime.UtcNow,
                Reference = string.IsNullOrWhiteSpace(model.Reference) ? "STOCK-IN" : model.Reference.Trim(),
                Notes = string.IsNullOrWhiteSpace(model.Notes) ? "Stock In replenishment" : model.Notes.Trim()
            };

            _context.StockTransactions.Add(transaction);
            await _context.SaveChangesAsync();

            await _audit.LogAsync("Stock In", "Inventory", $"Replenished +{model.Quantity} {supply.Unit} for '{supply.SupplyName}'");

            TempData["InventorySuccess"] = $"Stock In: Added +{model.Quantity} {supply.Unit} to '{supply.SupplyName}'. (New Stock: {supply.Quantity})";
            return RedirectToAction(nameof(DentalSupplies));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StockOut(StockMovementInputModel model)
        {
            if (model.Quantity <= 0)
            {
                TempData["InventoryError"] = "Quantity must be greater than zero.";
                return RedirectToAction(nameof(DentalSupplies));
            }

            var supply = await _context.Supplies.FindAsync(model.SupplyId);
            if (supply == null) return NotFound();

            if (supply.Quantity < model.Quantity)
            {
                TempData["InventoryError"] = $"Cannot Stock Out {model.Quantity} {supply.Unit}. Only {supply.Quantity} {supply.Unit} available!";
                return RedirectToAction(nameof(DentalSupplies));
            }

            supply.Quantity -= model.Quantity;
            supply.UpdatedAt = DateTime.UtcNow;
            if (supply.Status != "Archived")
            {
                supply.Status = supply.Quantity <= 0 ? "Out of Stock" : (supply.Quantity <= (supply.MinimumStock ?? 0) ? "Low Stock" : "In Stock");
            }

            var currentUser = await _userManager.GetUserAsync(User);
            var transaction = new StockTransaction
            {
                SupplyId = supply.Id,
                UserId = currentUser?.Id,
                TransactionType = "Out",
                Quantity = model.Quantity,
                TransactionDate = DateTime.UtcNow,
                Reference = string.IsNullOrWhiteSpace(model.Reference) ? "STOCK-OUT" : model.Reference.Trim(),
                Notes = string.IsNullOrWhiteSpace(model.Notes) ? "Used in clinic treatment/procedure" : model.Notes.Trim()
            };

            _context.StockTransactions.Add(transaction);
            await _context.SaveChangesAsync();

            await _audit.LogAsync("Stock Out", "Inventory", $"Deducted -{model.Quantity} {supply.Unit} from '{supply.SupplyName}'");

            TempData["InventorySuccess"] = $"Stock Out: Deducted -{model.Quantity} {supply.Unit} from '{supply.SupplyName}'. (Remaining Stock: {supply.Quantity})";
            return RedirectToAction(nameof(DentalSupplies));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ArchiveSupply(int id)
        {
            var supply = await _context.Supplies.FindAsync(id);
            if (supply == null) return NotFound();

            if (supply.Status == "Archived")
            {
                supply.Status = supply.Quantity <= 0 ? "Out of Stock" : (supply.Quantity <= (supply.MinimumStock ?? 0) ? "Low Stock" : "In Stock");
                TempData["InventorySuccess"] = $"Supply '{supply.SupplyName}' unarchived and restored to active inventory.";
            }
            else
            {
                supply.Status = "Archived";
                TempData["InventorySuccess"] = $"Supply '{supply.SupplyName}' archived.";
            }

            supply.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            await _audit.LogAsync("Archive Supply", "Inventory", $"Toggled archive state for supply '{supply.SupplyName}'");

            return RedirectToAction(nameof(DentalSupplies));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateCategory(string categoryName, string? description)
        {
            if (string.IsNullOrWhiteSpace(categoryName))
            {
                TempData["InventoryError"] = "Category name cannot be empty.";
                return RedirectToAction(nameof(DentalSupplies));
            }

            var exists = await _context.InventoryCategories
                .AnyAsync(c => c.CategoryName.ToLower() == categoryName.Trim().ToLower());

            if (exists)
            {
                TempData["InventoryError"] = $"Category '{categoryName.Trim()}' already exists.";
                return RedirectToAction(nameof(DentalSupplies));
            }

            var cat = new InventoryCategory
            {
                CategoryName = categoryName.Trim(),
                Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim()
            };

            _context.InventoryCategories.Add(cat);
            await _context.SaveChangesAsync();

            await _audit.LogAsync("Create Category", "Inventory", $"Added category '{cat.CategoryName}'");

            TempData["InventorySuccess"] = $"Category '{cat.CategoryName}' added successfully!";
            return RedirectToAction(nameof(DentalSupplies));
        }

        private static string GetComputedStatus(string status, int qty, int min)
        {
            if (status == "Archived") return "Archived";
            if (qty <= 0) return "Out";
            if (qty <= min) return "Low";
            return "Good";
        }

        private async Task SeedInitialInventoryIfEmptyAsync()
        {
            var requiredCategories = new[]
            {
                "Preventive", "Restorative", "Cosmetic", "Orthodontics",
                "Oral Surgery", "Gum Care", "Pediatric", "Emergency", "Specialized"
            };

            var existingCats = await _context.InventoryCategories.ToListAsync();
            var missingCats = requiredCategories
                .Where(rc => !existingCats.Any(c => c.CategoryName.Equals(rc, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            if (missingCats.Any())
            {
                foreach (var catName in missingCats)
                {
                    _context.InventoryCategories.Add(new InventoryCategory
                    {
                        CategoryName = catName,
                        Description = $"Dental clinical supplies for {catName} procedures"
                    });
                }
                await _context.SaveChangesAsync();
                existingCats = await _context.InventoryCategories.ToListAsync();
            }

            if (!await _context.Supplies.AnyAsync())
            {
                var getCatId = (string name) => existingCats.First(c => c.CategoryName.Equals(name, StringComparison.OrdinalIgnoreCase)).Id;

                var supplies = new List<Supply>
                {
                    // Preventive
                    new() { CategoryId = getCatId("Preventive"), SupplyName = "Dental Exam Mirrors & Probe Sets", Description = "Sterile diagnostic exam instruments", Unit = "Pack (10s)", Quantity = 50, MinimumStock = 10, PurchasePrice = 500.00m, Status = "In Stock", CreatedAt = DateTime.UtcNow },
                    new() { CategoryId = getCatId("Preventive"), SupplyName = "Prophylaxis Paste & Prophy Cups", Description = "Mint fluoride cleaning paste & rubber cups", Unit = "Jar (200g)", Quantity = 40, MinimumStock = 10, PurchasePrice = 650.00m, Status = "In Stock", CreatedAt = DateTime.UtcNow },
                    new() { CategoryId = getCatId("Preventive"), SupplyName = "Fluoride Varnish & Sealants", Description = "Enamel protection varnish kits", Unit = "Box (50s)", Quantity = 30, MinimumStock = 8, PurchasePrice = 1200.00m, Status = "In Stock", CreatedAt = DateTime.UtcNow },

                    // Restorative
                    new() { CategoryId = getCatId("Restorative"), SupplyName = "Composite Resin Capsule A2", Description = "Nano-hybrid tooth-colored filling resin", Unit = "Pack (20s)", Quantity = 35, MinimumStock = 10, PurchasePrice = 1450.00m, Status = "In Stock", CreatedAt = DateTime.UtcNow },
                    new() { CategoryId = getCatId("Restorative"), SupplyName = "Dental Crown Cement & Primers", Description = "Permanent luting cement for crowns & bridges", Unit = "Kit", Quantity = 25, MinimumStock = 5, PurchasePrice = 2800.00m, Status = "In Stock", CreatedAt = DateTime.UtcNow },
                    new() { CategoryId = getCatId("Restorative"), SupplyName = "Root Canal Gutta Percha & Files", Description = "Standardized endodontic canal filler points", Unit = "Box (60s)", Quantity = 20, MinimumStock = 5, PurchasePrice = 580.00m, Status = "In Stock", CreatedAt = DateTime.UtcNow },

                    // Cosmetic
                    new() { CategoryId = getCatId("Cosmetic"), SupplyName = "Teeth Whitening Gel Syringes 35%", Description = "In-office LED bleaching gel kit", Unit = "Kit (10s)", Quantity = 30, MinimumStock = 8, PurchasePrice = 3500.00m, Status = "In Stock", CreatedAt = DateTime.UtcNow },
                    new() { CategoryId = getCatId("Cosmetic"), SupplyName = "Dental Veneers Porcelain Bonding Kit", Description = "Aesthetic veneer resin cement kit", Unit = "Kit", Quantity = 15, MinimumStock = 5, PurchasePrice = 4500.00m, Status = "In Stock", CreatedAt = DateTime.UtcNow },
                    new() { CategoryId = getCatId("Cosmetic"), SupplyName = "Cosmetic Composite & Polishers", Description = "High-shine enamel finishing discs", Unit = "Pack", Quantity = 25, MinimumStock = 6, PurchasePrice = 1800.00m, Status = "In Stock", CreatedAt = DateTime.UtcNow },

                    // Orthodontics
                    new() { CategoryId = getCatId("Orthodontics"), SupplyName = "Metal Brackets & Archwire Sets", Description = "Standard stainless steel braces kit", Unit = "Set", Quantity = 25, MinimumStock = 8, PurchasePrice = 2500.00m, Status = "In Stock", CreatedAt = DateTime.UtcNow },
                    new() { CategoryId = getCatId("Orthodontics"), SupplyName = "Ceramic Brackets & Aesthetic Wires", Description = "Tooth-colored ceramic bracket set", Unit = "Set", Quantity = 20, MinimumStock = 5, PurchasePrice = 4200.00m, Status = "In Stock", CreatedAt = DateTime.UtcNow },
                    new() { CategoryId = getCatId("Orthodontics"), SupplyName = "Clear Aligner Sheets & Retainer Wire", Description = "Thermoforming aligner vacuum sheets", Unit = "Box (50s)", Quantity = 30, MinimumStock = 10, PurchasePrice = 1900.00m, Status = "In Stock", CreatedAt = DateTime.UtcNow },

                    // Oral Surgery
                    new() { CategoryId = getCatId("Oral Surgery"), SupplyName = "Extraction Forceps & Gauze Packs", Description = "Sterile surgical extraction supplies", Unit = "Set", Quantity = 40, MinimumStock = 10, PurchasePrice = 1500.00m, Status = "In Stock", CreatedAt = DateTime.UtcNow },
                    new() { CategoryId = getCatId("Oral Surgery"), SupplyName = "Titanium Dental Implants & Abutments", Description = "Biocompatible root fixture implants", Unit = "Box (5s)", Quantity = 15, MinimumStock = 5, PurchasePrice = 12000.00m, Status = "In Stock", CreatedAt = DateTime.UtcNow },
                    new() { CategoryId = getCatId("Oral Surgery"), SupplyName = "Bone Grafting Material & Membranes", Description = "Synthetic bone mineral matrix", Unit = "Vial", Quantity = 10, MinimumStock = 3, PurchasePrice = 4800.00m, Status = "In Stock", CreatedAt = DateTime.UtcNow },

                    // Gum Care
                    new() { CategoryId = getCatId("Gum Care"), SupplyName = "Periodontal Deep Cleaning Curettes", Description = "Subgingival scaling instruments", Unit = "Set", Quantity = 25, MinimumStock = 5, PurchasePrice = 1500.00m, Status = "In Stock", CreatedAt = DateTime.UtcNow },
                    new() { CategoryId = getCatId("Gum Care"), SupplyName = "Scaling & Root Planing Gel", Description = "Subgingival therapeutic gel", Unit = "Tube", Quantity = 30, MinimumStock = 8, PurchasePrice = 850.00m, Status = "In Stock", CreatedAt = DateTime.UtcNow },
                    new() { CategoryId = getCatId("Gum Care"), SupplyName = "Gum Surgery Scalpel Blades #15", Description = "Micro-surgical scalpel blades", Unit = "Box (100s)", Quantity = 40, MinimumStock = 10, PurchasePrice = 450.00m, Status = "In Stock", CreatedAt = DateTime.UtcNow },

                    // Pediatric
                    new() { CategoryId = getCatId("Pediatric"), SupplyName = "Children's Disposable Exam Kits", Description = "Child-sized mirrors & probes", Unit = "Pack (25s)", Quantity = 45, MinimumStock = 10, PurchasePrice = 750.00m, Status = "In Stock", CreatedAt = DateTime.UtcNow },
                    new() { CategoryId = getCatId("Pediatric"), SupplyName = "Children's Fluoride Varnish (Bubblegum)", Description = "Flavored pediatric fluoride gel", Unit = "Box (50s)", Quantity = 35, MinimumStock = 8, PurchasePrice = 1100.00m, Status = "In Stock", CreatedAt = DateTime.UtcNow },
                    new() { CategoryId = getCatId("Pediatric"), SupplyName = "Children's Glass Ionomer Filling", Description = "Biocompatible pediatric filling resin", Unit = "Box", Quantity = 30, MinimumStock = 8, PurchasePrice = 1650.00m, Status = "In Stock", CreatedAt = DateTime.UtcNow },

                    // Emergency
                    new() { CategoryId = getCatId("Emergency"), SupplyName = "Emergency Pain Relief Dressing & Gel", Description = "Immediate analgesic oral gel", Unit = "Bottle", Quantity = 30, MinimumStock = 8, PurchasePrice = 600.00m, Status = "In Stock", CreatedAt = DateTime.UtcNow },
                    new() { CategoryId = getCatId("Emergency"), SupplyName = "Lidocaine 2% Anesthetic Cartridges", Description = "Local dental anesthesia cartridges", Unit = "Box (50s)", Quantity = 50, MinimumStock = 15, PurchasePrice = 1850.00m, Status = "In Stock", CreatedAt = DateTime.UtcNow },
                    new() { CategoryId = getCatId("Emergency"), SupplyName = "Temporary Crown & Restoration Cement", Description = "Quick-setting emergency cement", Unit = "Kit", Quantity = 25, MinimumStock = 6, PurchasePrice = 950.00m, Status = "In Stock", CreatedAt = DateTime.UtcNow },

                    // Specialized
                    new() { CategoryId = getCatId("Specialized"), SupplyName = "TMJ Night Guard Sheets", Description = "Thermoplastic occlusal guard sheets", Unit = "Pack (20s)", Quantity = 30, MinimumStock = 8, PurchasePrice = 1250.00m, Status = "In Stock", CreatedAt = DateTime.UtcNow },
                    new() { CategoryId = getCatId("Specialized"), SupplyName = "Sleep Apnea Appliance Material", Description = "Mandibular repositioning kit", Unit = "Kit", Quantity = 15, MinimumStock = 5, PurchasePrice = 2900.00m, Status = "In Stock", CreatedAt = DateTime.UtcNow }
                };

                _context.Supplies.AddRange(supplies);
                await _context.SaveChangesAsync();

                foreach (var sup in supplies)
                {
                    _context.StockTransactions.Add(new StockTransaction
                    {
                        SupplyId = sup.Id,
                        TransactionType = "In",
                        Quantity = sup.Quantity,
                        TransactionDate = DateTime.UtcNow.AddDays(-Random.Shared.Next(1, 10)),
                        Reference = "PO-2026-INIT",
                        Notes = "Initial dental clinic inventory stock"
                    });
                }
                await _context.SaveChangesAsync();
            }
        }
    }
}
