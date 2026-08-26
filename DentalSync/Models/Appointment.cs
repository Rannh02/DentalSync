using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DentalSync.Models
{
    public class Appointment
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int PatientId { get; set; }

        [ForeignKey(nameof(PatientId))]
        public Patient Patient { get; set; } = null!;

        [Required]
        public int DentistId { get; set; }

        [ForeignKey(nameof(DentistId))]
        public Dentist Dentist { get; set; } = null!;

        [Required]
        public int ServiceId { get; set; }

        [ForeignKey(nameof(ServiceId))]
        public DentalService Service { get; set; } = null!;

        [Required]
        public DateOnly AppointmentDate { get; set; }

        [Required]
        public TimeOnly StartTime { get; set; }

        public TimeOnly? EndTime { get; set; }

        [Required]
        [StringLength(30)]
        public string Status { get; set; } = "Scheduled"; // Scheduled, Confirmed, Completed, Cancelled, NoShow

        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        // 1-to-1 or 1-to-many navigation properties
        public TreatmentRecord? TreatmentRecord { get; set; }
        public ICollection<Reminder> Reminders { get; set; } = new List<Reminder>();
    }
}
