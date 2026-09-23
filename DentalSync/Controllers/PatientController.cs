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

        public async Task<IActionResult> Dashboard()
        {
            await SetUserNameViewBagAsync();
            return View("~/Views/Patients/Dashboard.cshtml");
        }

        public async Task<IActionResult> ManageProfile()
        {
            await SetUserNameViewBagAsync();
            return View("~/Views/Patients/ManageProfile.cshtml");
        }

        public async Task<IActionResult> RequestAppointment()
        {
            await SetUserNameViewBagAsync();
            var user = await _userManager.GetUserAsync(User);
            var patient = user != null ? await _context.Patients.FirstOrDefaultAsync(p => p.UserId == user.Id) : null;

            var vm = new DentalSync.ViewModels.RequestAppointmentViewModel();

            if (patient != null)
            {
                var rawAppointments = await _context.Appointments
                    .Where(a => a.PatientId == patient.Id)
                    .Include(a => a.Dentist)
                    .Include(a => a.Service)
                    .OrderByDescending(a => a.AppointmentDate)
                    .ThenByDescending(a => a.StartTime)
                    .ToListAsync();

                var allServices = await _context.Services.ToListAsync();

                var list = new List<DentalSync.ViewModels.AppointmentListItemViewModel>();
                foreach (var a in rawAppointments)
                {
                    var serviceIds = new List<int>();
                    if (a.ServiceId > 0) serviceIds.Add(a.ServiceId);

                    if (!string.IsNullOrWhiteSpace(a.Notes) && a.Notes.StartsWith("[svc:"))
                    {
                        var closeBracket = a.Notes.IndexOf(']');
                        if (closeBracket > 5)
                        {
                            var rawIds = a.Notes.Substring(5, closeBracket - 5);
                            var parsedIds = rawIds.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                .Select(idStr => int.TryParse(idStr, out var val) ? val : 0)
                                .Where(val => val > 0);
                            serviceIds.AddRange(parsedIds);
                        }
                    }

                    var matchedServices = allServices.Where(s => serviceIds.Contains(s.Id)).ToList();
                    var serviceNames = matchedServices.Count > 0
                        ? string.Join(", ", matchedServices.Select(s => s.Name))
                        : (a.Service?.Name ?? "General Dentistry");
                    var totalCost = matchedServices.Count > 0
                        ? matchedServices.Sum(s => s.Cost)
                        : (a.Service?.Cost ?? 0m);

                    var dentistName = a.Dentist != null
                        ? $"Dr. {a.Dentist.FirstName} {a.Dentist.LastName}".Trim()
                        : "Assigned Dentist";

                    list.Add(new DentalSync.ViewModels.AppointmentListItemViewModel
                    {
                        Id = a.Id,
                        PatientId = a.PatientId,
                        PatientName = $"{patient.FirstName} {patient.LastName}",
                        DentistId = a.DentistId,
                        DentistName = dentistName,
                        ServiceId = a.ServiceId,
                        ServiceNames = serviceNames,
                        TotalCost = totalCost,
                        AppointmentDate = a.AppointmentDate,
                        StartTime = a.StartTime,
                        EndTime = a.EndTime,
                        Status = a.Status,
                        Notes = a.Notes
                    });
                }

                vm.MyAppointments = list;
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

        public async Task<IActionResult> ViewBillingPayments(string search = "", string status = "")
        {
            await SetUserNameViewBagAsync();
            var user = await _userManager.GetUserAsync(User);
            var patient = user != null ? await _context.Patients.FirstOrDefaultAsync(p => p.UserId == user.Id) : null;

            var invoices = new List<Invoice>();
            if (patient != null)
            {
                var query = _context.Invoices
                    .Where(i => i.PatientId == patient.Id)
                    .Include(i => i.InvoiceItems)
                    .Include(i => i.Payments)
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(search))
                {
                    var s = search.Trim().ToLower();
                    query = query.Where(i => i.InvoiceNumber.ToLower().Contains(s) ||
                                             i.InvoiceItems.Any(item => item.Description != null && item.Description.ToLower().Contains(s)));
                }

                if (!string.IsNullOrWhiteSpace(status))
                {
                    query = query.Where(i => i.Status.ToLower() == status.Trim().ToLower());
                }

                invoices = await query.OrderByDescending(i => i.InvoiceDate).ToListAsync();
            }

            ViewBag.Search = search;
            ViewBag.Status = status;

            return View("~/Views/Patients/ViewBillingPayments.cshtml", invoices);
        }

        public async Task<IActionResult> ViewTreatmentTransaction(string search = "", string status = "")
        {
            await SetUserNameViewBagAsync();
            var user = await _userManager.GetUserAsync(User);
            var patient = user != null ? await _context.Patients.FirstOrDefaultAsync(p => p.UserId == user.Id) : null;

            var appointmentsList = new List<DentalSync.ViewModels.AppointmentListItemViewModel>();

            if (patient != null)
            {
                var query = _context.Appointments
                    .Where(a => a.PatientId == patient.Id)
                    .Include(a => a.Dentist)
                    .Include(a => a.Service)
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(status))
                {
                    query = query.Where(a => a.Status.ToLower() == status.Trim().ToLower());
                }

                var rawAppointments = await query
                    .OrderByDescending(a => a.AppointmentDate)
                    .ThenByDescending(a => a.StartTime)
                    .ToListAsync();

                var allServices = await _context.Services.ToListAsync();

                foreach (var a in rawAppointments)
                {
                    var serviceIds = new List<int>();
                    if (a.ServiceId > 0) serviceIds.Add(a.ServiceId);

                    if (!string.IsNullOrWhiteSpace(a.Notes) && a.Notes.StartsWith("[svc:"))
                    {
                        var closeBracket = a.Notes.IndexOf(']');
                        if (closeBracket > 5)
                        {
                            var rawIds = a.Notes.Substring(5, closeBracket - 5);
                            var parsedIds = rawIds.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                .Select(idStr => int.TryParse(idStr, out var val) ? val : 0)
                                .Where(val => val > 0);
                            serviceIds.AddRange(parsedIds);
                        }
                    }

                    var matchedServices = allServices.Where(s => serviceIds.Contains(s.Id)).ToList();
                    var serviceNames = matchedServices.Count > 0
                        ? string.Join(", ", matchedServices.Select(s => s.Name))
                        : (a.Service?.Name ?? "General Dentistry");
                    var totalCost = matchedServices.Count > 0
                        ? matchedServices.Sum(s => s.Cost)
                        : (a.Service?.Cost ?? 0m);

                    if (!string.IsNullOrWhiteSpace(search))
                    {
                        var s = search.Trim().ToLower();
                        var dentistMatch = a.Dentist != null && $"{a.Dentist.FirstName} {a.Dentist.LastName}".ToLower().Contains(s);
                        var serviceMatch = serviceNames.ToLower().Contains(s);
                        var notesMatch = a.Notes != null && a.Notes.ToLower().Contains(s);

                        if (!dentistMatch && !serviceMatch && !notesMatch)
                        {
                            continue;
                        }
                    }

                    var dentistName = a.Dentist != null
                        ? $"Dr. {a.Dentist.FirstName} {a.Dentist.LastName}".Trim()
                        : "Assigned Dentist";

                    appointmentsList.Add(new DentalSync.ViewModels.AppointmentListItemViewModel
                    {
                        Id = a.Id,
                        PatientId = a.PatientId,
                        PatientName = $"{patient.FirstName} {patient.LastName}",
                        DentistId = a.DentistId,
                        DentistName = dentistName,
                        ServiceId = a.ServiceId,
                        ServiceNames = serviceNames,
                        TotalCost = totalCost,
                        AppointmentDate = a.AppointmentDate,
                        StartTime = a.StartTime,
                        EndTime = a.EndTime,
                        Status = a.Status,
                        Notes = a.Notes
                    });
                }
            }

            ViewBag.Search = search;
            ViewBag.Status = status;

            return View("~/Views/Patients/ViewTreatmentTransaction.cshtml", appointmentsList);
        }

        private async Task SetUserNameViewBagAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            ViewBag.UserFullName = user?.FullName ?? user?.UserName ?? "there";
        }
    }
}
