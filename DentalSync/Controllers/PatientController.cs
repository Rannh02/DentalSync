using DentalSync.Data;
using DentalSync.Models;
using DentalSync.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DentalSync.Controllers
{
    [Authorize(Roles = "Patient")]
    public class PatientController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<Users> _userManager;
        private readonly AuditService _audit;

        public PatientController(AppDbContext context, UserManager<Users> userManager, AuditService audit)
        {
            _context = context;
            _userManager = userManager;
            _audit = audit;
        }

        public IActionResult Dashboard()
        {
            return View("~/Views/Patients/Dashboard.cshtml");
        }

        public IActionResult ManageProfile()
        {
            return View("~/Views/Patients/ManageProfile.cshtml");
        }

        public IActionResult RequestAppointment()
        {
            return View("~/Views/Patients/RequestAppointment.cshtml");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestAppointmentPost(int serviceId, DateOnly appointmentDate, TimeSpan startTime, string notes)
        {
            var user = await _userManager.GetUserAsync(User);
            var patient = await _context.Patients.FirstOrDefaultAsync(p => p.UserId == user.Id);

            if (patient != null)
            {
                var appointment = new Appointment
                {
                    PatientId = patient.Id,
                    ServiceId = serviceId,
                    AppointmentDate = appointmentDate,
                    StartTime = TimeOnly.FromTimeSpan(startTime),
                    Status = "Pending",
                    Notes = notes,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Appointments.Add(appointment);
                await _context.SaveChangesAsync();

                await _audit.LogAsync("Request Appointment", "Patient Portal", $"Patient requested an appointment booking for {appointmentDate:yyyy-MM-dd} at {startTime}");
                TempData["PatientSuccess"] = "Appointment request submitted successfully!";
            }

            return RedirectToAction(nameof(Dashboard));
        }

        public IActionResult ViewBillingPayments()
        {
            return View("~/Views/Patients/ViewBillingPayments.cshtml");
        }

        public IActionResult ReceiveReminders()
        {
            return View("~/Views/Patients/ReceiveReminders.cshtml");
        }

        public IActionResult ViewTreatmentTransaction()
        {
            return View("~/Views/Patients/ViewTreatmentTransaction.cshtml");
        }
    }
}
