using DentalSync.Data;
using DentalSync.Models;
using DentalSync.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DentalSync.Controllers
{
    [Authorize(Roles = "Dentist")]
    public class DentistController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<Users> _userManager;
        private readonly AuditService _audit;

        public DentistController(AppDbContext context, UserManager<Users> userManager, AuditService audit)
        {
            _context = context;
            _userManager = userManager;
            _audit = audit;
        }

        public IActionResult Dashboard()
        {
            return View("~/Views/Dentist/Dashboard.cshtml");
        }

        public IActionResult ViewPatientRecords()
        {
            return View("~/Views/Dentist/ViewPatientRecords.cshtml");
        }

        public IActionResult ViewAppointments()
        {
            return View("~/Views/Dentist/ViewAppointments.cshtml");
        }

        public async Task<IActionResult> UpdateDentalHistory()
        {
            await _audit.LogAsync("View Dental History", "Medical Records", "Dentist accessed patient dental history records");
            return View("~/Views/Dentist/UpdateDentalHistory.cshtml");
        }

        public async Task<IActionResult> TreatmentRecords()
        {
            await _audit.LogAsync("View Treatment Records", "Medical Records", "Dentist accessed treatment history logs");
            return View("~/Views/Dentist/TreatmentRecords.cshtml");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CompleteAppointment(int appointmentId, string notes)
        {
            var appointment = await _context.Appointments
                .Include(a => a.Patient)
                .FirstOrDefaultAsync(a => a.Id == appointmentId);

            if (appointment != null)
            {
                appointment.Status = "Completed";
                appointment.Notes = notes;
                appointment.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                var patientName = appointment.Patient != null ? $"{appointment.Patient.FirstName} {appointment.Patient.LastName}" : $"Patient #{appointment.PatientId}";
                await _audit.LogAsync("Complete Appointment", "Appointments", $"Dentist completed appointment for {patientName}");
            }

            return RedirectToAction(nameof(ViewAppointments));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddTreatmentRecord(int patientId, int serviceId, string notes)
        {
            var patient = await _context.Patients.FindAsync(patientId);
            var service = await _context.Services.FindAsync(serviceId);

            var treatment = new TreatmentRecord
            {
                PatientId = patientId,
                ServiceId = serviceId,
                TreatmentDate = DateTime.UtcNow,
                Notes = notes,
                CreatedAt = DateTime.UtcNow
            };

            _context.TreatmentRecords.Add(treatment);
            await _context.SaveChangesAsync();

            var patientName = patient != null ? $"{patient.FirstName} {patient.LastName}" : $"Patient #{patientId}";
            var serviceName = service != null ? service.Name : "Dental Procedure";
            await _audit.LogAsync("Record Treatment", "Medical Records", $"Dentist recorded treatment '{serviceName}' for {patientName}");

            return RedirectToAction(nameof(TreatmentRecords));
        }
    }
}
