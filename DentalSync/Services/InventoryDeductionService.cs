using DentalSync.Data;
using DentalSync.Models;
using Microsoft.EntityFrameworkCore;

namespace DentalSync.Services
{
    public class InventoryDeductionService
    {
        private readonly AppDbContext _context;

        public InventoryDeductionService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<bool> DeductSupplyForCategoryAsync(string? categoryName, string serviceName, string? userId = null)
        {
            if (string.IsNullOrWhiteSpace(categoryName))
                return false;

            var catName = categoryName.Trim();
            var category = await _context.InventoryCategories
                .FirstOrDefaultAsync(c => EF.Functions.Like(c.CategoryName, catName));

            if (category == null)
                return false;

            var supply = await _context.Supplies
                .Where(s => s.CategoryId == category.Id && s.Status != "Archived" && s.Quantity > 0)
                .OrderBy(s => s.Quantity)
                .FirstOrDefaultAsync();

            if (supply == null)
                return false;

            supply.Quantity -= 1;
            supply.UpdatedAt = DateTime.UtcNow;
            supply.Status = supply.Quantity <= 0 ? "Out of Stock" : (supply.Quantity <= (supply.MinimumStock ?? 0) ? "Low Stock" : "In Stock");

            var transaction = new StockTransaction
            {
                SupplyId = supply.Id,
                UserId = userId,
                TransactionType = "Out",
                Quantity = 1,
                TransactionDate = DateTime.UtcNow,
                Reference = "AUTO-DEDUCT",
                Notes = $"Used for service: {serviceName} ({catName})"
            };

            _context.StockTransactions.Add(transaction);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
