using System.ComponentModel.DataAnnotations;

namespace DentalSync.Models
{
    public class AuditLog
    {
        public int Id { get; set; }

        [Required]
        public DateTime DateTime { get; set; } = DateTime.Now;

        [MaxLength(256)]
        public string User { get; set; } = "";

        [MaxLength(256)]
        public string Role { get; set; } = "";

        [MaxLength(100)]
        public string Action { get; set; } = "";

        [MaxLength(100)]
        public string Module { get; set; } = "";

        [MaxLength(1000)]
        public string Description { get; set; } = "";

        [MaxLength(64)]
        public string IpAddress { get; set; } = "";

        [MaxLength(100)]
        public string Browser { get; set; } = "";
    }
}
