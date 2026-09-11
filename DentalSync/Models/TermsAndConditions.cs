using System;
using System.ComponentModel.DataAnnotations;

namespace DentalSync.Models
{
    public class TermsAndConditions
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string Title { get; set; } = "DentalSync Subscription Terms & Conditions";

        [Required]
        public string Content { get; set; } = string.Empty;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [StringLength(150)]
        public string? UpdatedBy { get; set; }
    }
}
