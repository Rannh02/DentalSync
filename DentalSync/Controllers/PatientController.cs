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

        public async Task<IActionResult> RequestAppointment()
        {
            var user = await _userManager.GetUserAsync(User);
            var patient = await _context.Patients.FirstOrDefaultAsync(p => p.UserId == user!.Id);

            var vm = new DentalSync.ViewModels.RequestAppointmentViewModel();

            if (patient != null)
            {
                vm.MyAppointments = await _context.Appointments
                    .Where(a => a.PatientId == patient.Id)
                    .Include(a => a.Dentist)
                    .Include(a => a.Service)
                    .OrderByDescending(a => a.AppointmentDate)
                    .ThenByDescending(a => a.StartTime)
                    .Select(a => new DentalSync.ViewModels.AppointmentListItemViewModel
                    {
                        Id = a.Id,
                        PatientId = a.PatientId,
                        PatientName = patient.FirstName + " " + patient.LastName,
                        DentistId = a.DentistId,
                        DentistName = "Dr. " + a.Dentist.FirstName + " " + a.Dentist.LastName,
                        ServiceId = a.ServiceId,
                        ServiceNames = a.Service.Name,
                        TotalCost = a.Service.Cost,
                        AppointmentDate = a.AppointmentDate,
                        StartTime = a.StartTime,
                        EndTime = a.EndTime,
                        Status = a.Status,
                        Notes = a.Notes
                    })
                    .ToListAsync();
            }

            return View("~/Views/Patients/RequestAppointment.cshtml", vm);
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
