using DentalSync.Models;

namespace DentalSync.ViewModels
{
    public class DentalSuppliesViewModel
    {
        public List<SupplyItemViewModel> Supplies { get; set; } = new();
        public List<InventoryCategory> Categories { get; set; } = new();

        // 4 KPIs
        public int TotalItems { get; set; }
        public int InStockCount { get; set; }    // Good
        public int LowStockCount { get; set; }   // Low
        public int OutOfStockCount { get; set; } // Out

        // Filters
        public string? Search { get; set; }
        public int? CategoryId { get; set; }
        public string? Status { get; set; } // "all", "good", "low", "out", "archived"
        public string? Sort { get; set; }

        // Pagination
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int TotalPages { get; set; }
        public int TotalCount { get; set; }
    }

    public class SupplyItemViewModel
    {
        public int Id { get; set; }
        public string SupplyName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public string? Unit { get; set; }
        public int Quantity { get; set; }
        public int MinimumStock { get; set; }
        public decimal? PurchasePrice { get; set; }
        public DateOnly? ExpirationDate { get; set; }
        public string ComputedStatus { get; set; } = "Good"; // "Good", "Low", "Out", "Archived"
        public string RawStatus { get; set; } = "In Stock";
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class StockTransactionsViewModel
    {
        public List<StockTransactionItemViewModel> Transactions { get; set; } = new();
        public List<Supply> Supplies { get; set; } = new();

        // Filters
        public string? Search { get; set; }
        public string? Type { get; set; } // "all", "In", "Out", "Adjustment"
        public string? Date { get; set; }
        public int? SupplyId { get; set; }

        // Pagination
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 15;
        public int TotalPages { get; set; }
        public int TotalCount { get; set; }
    }

    public class StockTransactionItemViewModel
    {
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public int SupplyId { get; set; }
        public string SupplyName { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public string Unit { get; set; } = "pcs";
        public string Type { get; set; } = "In"; // In, Out, Adjustment
        public int Quantity { get; set; }
        public string UserName { get; set; } = "System";
        public string? Reference { get; set; }
        public string? Notes { get; set; }
    }

    public class CreateSupplyInputModel
    {
        public int CategoryId { get; set; }
        public string SupplyName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Unit { get; set; } = "pcs";
        public int InitialQuantity { get; set; } = 0;
        public int MinimumStock { get; set; } = 10;
        public decimal? PurchasePrice { get; set; }
        public DateOnly? ExpirationDate { get; set; }
    }

    public class EditSupplyInputModel
    {
        public int Id { get; set; }
        public int CategoryId { get; set; }
        public string SupplyName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Unit { get; set; }
        public int MinimumStock { get; set; }
        public decimal? PurchasePrice { get; set; }
        public DateOnly? ExpirationDate { get; set; }
    }

    public class StockMovementInputModel
    {
        public int SupplyId { get; set; }
        public int Quantity { get; set; }
        public string? Reference { get; set; }
        public string? Notes { get; set; }
    }
}
