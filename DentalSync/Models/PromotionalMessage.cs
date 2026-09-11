using System.ComponentModel.DataAnnotations;

namespace DentalSync.Models
{
    public class PromotionalMessage
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [StringLength(150)]
        public string Email { get; set; } = string.Empty;

        [Required]
        [StringLength(30)]
        public string Phone { get; set; } = string.Empty;

        public DateTime? PreferredDate { get; set; }

        [Required]
        public string Message { get; set; } = string.Empty;

        [StringLength(30)]
        public string Status { get; set; } = "New";

        public string? ReceptionistNotes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }
    }
}
