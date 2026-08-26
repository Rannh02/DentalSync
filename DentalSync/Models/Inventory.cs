using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DentalSync.Models
{
    public class InventoryCategory
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string CategoryName { get; set; } = string.Empty;

        [StringLength(255)]
        public string? Description { get; set; }

        // Navigation properties
        public ICollection<Supply> Supplies { get; set; } = new List<Supply>();
    }

    public class Supply
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int CategoryId { get; set; }

        [ForeignKey(nameof(CategoryId))]
        public InventoryCategory Category { get; set; } = null!;

        [Required]
        [StringLength(150)]
        public string SupplyName { get; set; } = string.Empty;

        [StringLength(255)]
        public string? Description { get; set; }

        [StringLength(50)]
        public string? Unit { get; set; }

        [Required]
        public int Quantity { get; set; } = 0;

        public int? MinimumStock { get; set; } = 10;

        [Column(TypeName = "decimal(18,2)")]
        public decimal? PurchasePrice { get; set; }

        public DateOnly? ExpirationDate { get; set; }

        [Required]
        [StringLength(20)]
        public string Status { get; set; } = "In Stock"; // In Stock, Low Stock, Out of Stock, Expired

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        // Navigation properties
        public ICollection<StockTransaction> StockTransactions { get; set; } = new List<StockTransaction>();
    }

    public class StockTransaction
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int SupplyId { get; set; }

        [ForeignKey(nameof(SupplyId))]
        public Supply Supply { get; set; } = null!;

        public string? UserId { get; set; }

        [ForeignKey(nameof(UserId))]
        public Users? User { get; set; }

        [Required]
        [StringLength(30)]
        public string TransactionType { get; set; } = "In"; // In, Out, Adjustment

        [Required]
        public int Quantity { get; set; }

        [Required]
        public DateTime TransactionDate { get; set; } = DateTime.UtcNow;

        [StringLength(100)]
        public string? Reference { get; set; }

        public string? Notes { get; set; }
    }
}
