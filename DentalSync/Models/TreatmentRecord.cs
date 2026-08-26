using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DentalSync.Models
{
    public class TreatmentRecord
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int AppointmentId { get; set; }

        [ForeignKey(nameof(AppointmentId))]
        public Appointment Appointment { get; set; } = null!;

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

        public string? Diagnosis { get; set; }

        public string? Procedure { get; set; }

        public string? Notes { get; set; }

        [Required]
        public DateTime TreatmentDate { get; set; } = DateTime.UtcNow;

        public DateOnly? NextVisitDate { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }
    }
}
