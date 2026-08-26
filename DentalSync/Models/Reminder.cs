using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DentalSync.Models
{
    public class Reminder
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int PatientId { get; set; }

        [ForeignKey(nameof(PatientId))]
        public Patient Patient { get; set; } = null!;

        public int? AppointmentId { get; set; }

        [ForeignKey(nameof(AppointmentId))]
        public Appointment? Appointment { get; set; }

        [Required]
        [StringLength(50)]
        public string ReminderType { get; set; } = "SMS"; // SMS, Email, Call

        public string? Message { get; set; }

        [Required]
        public DateTime ReminderDate { get; set; } = DateTime.UtcNow;

        [StringLength(30)]
        public string? SentVia { get; set; }

        [Required]
        [StringLength(30)]
        public string Status { get; set; } = "Pending"; // Pending, Sent, Failed

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
