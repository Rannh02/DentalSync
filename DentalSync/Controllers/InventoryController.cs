using DentalSync.Data;
using DentalSync.Models;
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

        public InventoryController(AppDbContext context, UserManager<Users> userManager)
        {
            _context = context;
            _userManager = userManager;
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

            // All tracked active/non-deleted supplies for KPIs
            var allSupplies = await _context.Supplies
                .Include(s => s.Category)
                .AsNoTracking()
                .ToListAsync();

            // Compute the 4 KPIs
            var totalItems = allSupplies.Count;
            var inStockCount = allSupplies.Count(s => s.Status != "Archived" && s.Quantity > (s.MinimumStock ?? 0));
            var lowStockCount = allSupplies.Count(s => s.Status != "Archived" && s.Quantity > 0 && s.Quantity <= (s.MinimumStock ?? 0));
            var outOfStockCount = allSupplies.Count(s => s.Status != "Archived" && s.Quantity <= 0);

            // Filter Query
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

            // Status filter: "good", "low", "out", "archived"
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

            // Sorting
            query = sort switch
            {
                "name_asc" => query.OrderBy(s => s.SupplyName),
                "name_desc" => query.OrderByDescending(s => s.SupplyName),
                "stock_asc" => query.OrderBy(s => s.Quantity),
                "stock_desc" => query.OrderByDescending(s => s.Quantity),
                "exp_asc" => query.OrderBy(s => s.ExpirationDate ?? DateOnly.MaxValue),
                _ => query.OrderBy(s => s.SupplyName)
            };

            const int pageSize = 5;
            var totalFiltered = await query.CountAsync();
            var currentPage = Math.Max(1, page);

            var items = await query
                .Skip((currentPage - 1) * pageSize)
                .Take(pageSize)
                .Select(s => new SupplyItemViewModel
                {
                    Id = s.Id,
                    SupplyName = s.SupplyName,
                    Description = s.Description,
                    CategoryId = s.CategoryId,
                    CategoryName = s.Category.CategoryName,
                    Unit = s.Unit ?? "pcs",
                    Quantity = s.Quantity,
                    MinimumStock = s.MinimumStock ?? 10,
                    PurchasePrice = s.PurchasePrice,
                    ExpirationDate = s.ExpirationDate,
                    RawStatus = s.Status,
                    ComputedStatus = s.Status == "Archived"
                        ? "Archived"
                        : (s.Quantity <= 0 ? "Out" : (s.Quantity <= (s.MinimumStock ?? 0) ? "Low" : "Good")),
                    CreatedAt = s.CreatedAt,
                    UpdatedAt = s.UpdatedAt
                })
                .ToListAsync();

            var viewModel = new DentalSuppliesViewModel
            {
                Supplies = items,
                Categories = categories,
                TotalItems = totalItems,
                InStockCount = inStockCount,
                LowStockCount = lowStockCount,
                OutOfStockCount = outOfStockCount,
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
        // 2. Stock Transactions Section (History of Stock Movement)
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

            if (!string.IsNullOrWhiteSpace(type) && !type.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(st => st.TransactionType == type);
            }

            if (supplyId.HasValue && supplyId.Value > 0)
            {
                query = query.Where(st => st.SupplyId == supplyId.Value);
            }

            if (!string.IsNullOrWhiteSpace(date) && DateOnly.TryParse(date, out var parsedDate))
            {
                var start = parsedDate.ToDateTime(TimeOnly.MinValue);
                var end = parsedDate.ToDateTime(TimeOnly.MaxValue);
                query = query.Where(st => st.TransactionDate >= start && st.TransactionDate <= end);
            }

            const int pageSize = 6;
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

            // Record initial stock transaction if quantity > 0
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

            TempData["InventorySuccess"] = $"Category '{cat.CategoryName}' added successfully!";
            return RedirectToAction(nameof(DentalSupplies));
        }

        // =========================================================================
        // Seed Initial Categories & Supplies if database is fresh
        // =========================================================================
        private async Task SeedInitialInventoryIfEmptyAsync()
        {
            if (!await _context.InventoryCategories.AnyAsync())
            {
                var catPpe = new InventoryCategory { CategoryName = "PPE & Disposables", Description = "Gloves, masks, bibs, barriers" };
                var catRest = new InventoryCategory { CategoryName = "Restorative & Filling", Description = "Composites, etchants, primers, bonding agents" };
                var catAnesth = new InventoryCategory { CategoryName = "Anesthetics", Description = "Local anesthetics, needles, topical gels" };
                var catEndo = new InventoryCategory { CategoryName = "Endodontics", Description = "Root canal files, sealers, gutta percha" };
                var catPrev = new InventoryCategory { CategoryName = "Preventive & Hygiene", Description = "Prophy paste, fluoride varnish, sealants" };

                _context.InventoryCategories.AddRange(catPpe, catRest, catAnesth, catEndo, catPrev);
                await _context.SaveChangesAsync();

                var supplies = new List<Supply>
                {
                    new() { CategoryId = catPpe.Id, SupplyName = "Nitrile Examination Gloves (Medium)", Description = "Powder-free medical grade blue nitrile", Unit = "Box (100s)", Quantity = 45, MinimumStock = 15, PurchasePrice = 320.00m, ExpirationDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(18)), Status = "In Stock", CreatedAt = DateTime.UtcNow },
                    new() { CategoryId = catPpe.Id, SupplyName = "Dental Patient Bibs (3-ply)", Description = "Waterproof embossed patient bibs (Lavender)", Unit = "Pack (125s)", Quantity = 30, MinimumStock = 10, PurchasePrice = 180.00m, ExpirationDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(24)), Status = "In Stock", CreatedAt = DateTime.UtcNow },
                    new() { CategoryId = catRest.Id, SupplyName = "Composite Resin Capsule A2", Description = "Universal nano-hybrid light cure composite", Unit = "Pack (20s)", Quantity = 8, MinimumStock = 10, PurchasePrice = 1450.00m, ExpirationDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(12)), Status = "Low Stock", CreatedAt = DateTime.UtcNow },
                    new() { CategoryId = catRest.Id, SupplyName = "Phosphoric Acid Etchant Gel 37%", Description = "Syringe dispenser with applicator tips", Unit = "Syringe (12g)", Quantity = 14, MinimumStock = 5, PurchasePrice = 420.00m, ExpirationDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(15)), Status = "In Stock", CreatedAt = DateTime.UtcNow },
                    new() { CategoryId = catAnesth.Id, SupplyName = "Lidocaine HCl 2% with Epinephrine 1:100,000", Description = "Dental cartridges for local anesthesia", Unit = "Box (50s)", Quantity = 25, MinimumStock = 12, PurchasePrice = 1850.00m, ExpirationDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(10)), Status = "In Stock", CreatedAt = DateTime.UtcNow },
                    new() { CategoryId = catAnesth.Id, SupplyName = "Dental Needles 30G Short", Description = "Ultra-sharp disposable dental needles", Unit = "Box (100s)", Quantity = 4, MinimumStock = 8, PurchasePrice = 290.00m, ExpirationDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(20)), Status = "Low Stock", CreatedAt = DateTime.UtcNow },
                    new() { CategoryId = catEndo.Id, SupplyName = "Gutta Percha Points #25 0.04 Taper", Description = "Color-coded standardized root canal filler", Unit = "Box (60s)", Quantity = 0, MinimumStock = 5, PurchasePrice = 580.00m, ExpirationDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(16)), Status = "Out of Stock", CreatedAt = DateTime.UtcNow },
                    new() { CategoryId = catPrev.Id, SupplyName = "Prophylaxis Paste Medium Mint", Description = "Fluoride-enriched splatter-free cleaning paste", Unit = "Jar (200g)", Quantity = 12, MinimumStock = 4, PurchasePrice = 650.00m, ExpirationDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(14)), Status = "In Stock", CreatedAt = DateTime.UtcNow }
                };

                _context.Supplies.AddRange(supplies);
                await _context.SaveChangesAsync();

                // Add initial stock transactions
                foreach (var sup in supplies.Where(s => s.Quantity > 0))
                {
                    _context.StockTransactions.Add(new StockTransaction
                    {
                        SupplyId = sup.Id,
                        TransactionType = "In",
                        Quantity = sup.Quantity,
                        TransactionDate = DateTime.UtcNow.AddDays(-Random.Shared.Next(1, 10)),
                        Reference = "PO-2026-INIT",
                        Notes = "Initial clinic supply stock"
                    });
                }
                await _context.SaveChangesAsync();
            }
        }
    }
}
