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

        public async Task<IActionResult> Dashboard()
        {
            var user = await _userManager.GetUserAsync(User);
            ViewBag.UserFullName = user?.FullName ?? user?.UserName ?? "Doctor";

            var dentist = user == null
                ? null
                : await _context.Dentists.FirstOrDefaultAsync(d => d.UserId == user.Id);

            var model = new DentalSync.ViewModels.DentistDashboardViewModel();
            if (dentist != null)
            {
                var appointments = _context.Appointments
                    .Where(a => a.DentistId == dentist.Id && a.Status != "Archived");

                model.TotalPatients = await appointments
                    .Select(a => a.PatientId)
                    .Distinct()
                    .CountAsync();
                model.TodayAppointmentsCount = await appointments
                    .CountAsync(a => a.AppointmentDate == DateOnly.FromDateTime(DateTime.Today));
                model.TotalAppointmentsCount = await appointments.CountAsync();
                model.CompletedAppointmentsCount = await appointments
                    .CountAsync(a => a.Status == "Completed");

                var today = DateOnly.FromDateTime(DateTime.Today);
                model.TodayAppointments = await appointments
                    .Where(a => a.AppointmentDate == today)
                    .Include(a => a.Patient)
                    .Include(a => a.Service)
                    .OrderBy(a => a.StartTime)
                    .Take(8)
                    .Select(a => new DentalSync.ViewModels.DashboardAppointmentItemViewModel
                    {
                        Id = a.Id,
                        PatientName = a.Patient.FirstName + " " + a.Patient.LastName,
                        Initials = (a.Patient.FirstName.Substring(0, 1) + a.Patient.LastName.Substring(0, 1)).ToUpper(),
                        ServiceName = a.Service.Name,
                        DentistName = "Dr. " + dentist.FirstName + " " + dentist.LastName,
                        AppointmentDate = a.AppointmentDate,
                        StartTime = a.StartTime,
                        EndTime = a.EndTime,
                        CreatedAt = a.CreatedAt,
                        Status = a.Status
                    })
                    .ToListAsync();
            }

            return View("~/Views/Dentist/Dashboard.cshtml", model);
        }

        public async Task<IActionResult> ViewPatientRecords(string search = "", string statusFilter = "", int page = 1)
        {
            var user = await _userManager.GetUserAsync(User);
            var dentist = user == null
                ? null
                : await _context.Dentists.FirstOrDefaultAsync(d => d.UserId == user.Id);

            const int pageSize = 5;
            var model = new DentalSync.ViewModels.DentistPatientRecordsViewModel
            {
                Search = search,
                StatusFilter = statusFilter,
                Page = Math.Max(1, page),
                PageSize = pageSize
            };
            if (dentist != null)
            {
                var query = _context.Appointments
                    .Where(a => a.DentistId == dentist.Id && a.Status != "Archived")
                    .Include(a => a.Patient)
                    .Include(a => a.Dentist)
                    .Include(a => a.Service)
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(search))
                {
                    var term = search.Trim();
                    query = query.Where(a =>
                        EF.Functions.Like(a.Patient.FirstName, $"%{term}%") ||
                        EF.Functions.Like(a.Patient.LastName, $"%{term}%") ||
                        EF.Functions.Like(a.Patient.ContactNumber, $"%{term}%") ||
                        EF.Functions.Like(a.Service.Name, $"%{term}%"));
                }

                if (!string.IsNullOrWhiteSpace(statusFilter))
                    query = query.Where(a => a.Status == statusFilter);

                model.TotalRecords = await query.CountAsync();
                model.Records = await query
                    .OrderByDescending(a => a.AppointmentDate)
                    .ThenByDescending(a => a.StartTime)
                    .Skip((model.Page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(a => new DentalSync.ViewModels.DentistPatientRecordItemViewModel
                    {
                        AppointmentId = a.Id,
                        PatientId = a.PatientId,
                        PatientName = a.Patient.FirstName + " " + a.Patient.LastName,
                        DentistName = "Dr. " + a.Dentist.FirstName + " " + a.Dentist.LastName,
                        ServiceName = a.Service.Name,
                        AppointmentDate = a.AppointmentDate,
                        StartTime = a.StartTime,
                        EndTime = a.EndTime,
                        Status = a.Status,
                        Notes = a.Notes
                        })
                        .ToListAsync();
            }

            return View("~/Views/Dentist/ViewPatientRecords.cshtml", model);
        }

        public async Task<IActionResult> TransferRequests(string search = "", string statusFilter = "", int page = 1)
        {
            var user = await _userManager.GetUserAsync(User);
            var dentist = user == null
                ? null
                : await _context.Dentists.FirstOrDefaultAsync(d => d.UserId == user.Id);

            const int pageSize = 5;
            var model = new DentalSync.ViewModels.DentistTransferRequestsViewModel
            {
                Search = search,
                StatusFilter = statusFilter,
                Page = Math.Max(1, page),
                PageSize = pageSize
            };
            if (dentist == null)
                return View("TransferRequests", model);

            var requestLogs = await _context.AuditLogs
                .AsNoTracking()
                .Where(log => log.Action == "Request Patient Transfer" && log.Description.Contains($"[source-dentist:{dentist.Id}]"))
                .OrderByDescending(log => log.DateTime)
                .ToListAsync();
            var approvedAppointmentIds = await _context.AuditLogs
                .AsNoTracking()
                .Where(log => log.Action == "Approve Patient Transfer" && log.Description.Contains($"[from-dentist:{dentist.Id}]"))
                .Select(log => log.Description)
                .ToListAsync();
            var approvedIds = approvedAppointmentIds
                .Select(description => ExtractMarkerId(description, "approved-transfer"))
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .ToHashSet();

            var requestItems = requestLogs
                .Select(log => new { Log = log, AppointmentId = ExtractMarkerId(log.Description, "appointment") })
                .Where(item => item.AppointmentId.HasValue && !approvedIds.Contains(item.AppointmentId.Value))
                .GroupBy(item => item.AppointmentId!.Value)
                .Select(group => group.First())
                .ToList();
            var appointmentIds = requestItems.Select(item => item.AppointmentId!.Value).ToList();
            var appointments = await _context.Appointments
                .AsNoTracking()
                .Include(a => a.Patient)
                .Include(a => a.Service)
                .Where(a => appointmentIds.Contains(a.Id) && a.DentistId == dentist.Id)
                .ToDictionaryAsync(a => a.Id);

            model.Requests = requestItems
                .Where(item => appointments.ContainsKey(item.AppointmentId!.Value))
                .Select(item =>
                {
                    var appointment = appointments[item.AppointmentId!.Value];
                    return new DentalSync.ViewModels.DentistTransferRequestItemViewModel
                    {
                        AuditLogId = item.Log.Id,
                        AppointmentId = appointment.Id,
                        PatientId = appointment.PatientId,
                        PatientName = $"{appointment.Patient.FirstName} {appointment.Patient.LastName}",
                        ServiceName = appointment.Service.Name,
                        AppointmentDate = appointment.AppointmentDate,
                        StartTime = appointment.StartTime,
                        RequestedAt = item.Log.DateTime
                    };
                })
                .ToList();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                model.Requests = model.Requests
                    .Where(request => request.PatientName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                                      request.ServiceName.Contains(term, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            model.TotalRequests = model.Requests.Count;
            model.Requests = model.Requests
                .Skip((model.Page - 1) * pageSize)
                .Take(pageSize)
                .ToList();
            model.Dentists = await _context.Dentists
                .Where(d => d.Status == "Active" && d.Id != dentist.Id)
                .OrderBy(d => d.LastName)
                .ThenBy(d => d.FirstName)
                .ToListAsync();

            return View("TransferRequests", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApprovePatientTransfer(int appointmentId, int targetDentistId)
        {
            var user = await _userManager.GetUserAsync(User);
            var sourceDentist = user == null
                ? null
                : await _context.Dentists.FirstOrDefaultAsync(d => d.UserId == user.Id);
            var targetDentist = await _context.Dentists
                .FirstOrDefaultAsync(d => d.Id == targetDentistId && d.Status == "Active");
            var appointment = await _context.Appointments
                .Include(a => a.Patient)
                .Include(a => a.Service)
                .FirstOrDefaultAsync(a => a.Id == appointmentId);

            if (sourceDentist == null || targetDentist == null || appointment == null || appointment.DentistId != sourceDentist.Id || targetDentist.Id == sourceDentist.Id)
                return NotFound();

            var hasRequest = await _context.AuditLogs.AnyAsync(log =>
                log.Action == "Request Patient Transfer" &&
                log.Description.Contains($"[appointment:{appointmentId}]") &&
                log.Description.Contains($"[source-dentist:{sourceDentist.Id}]"));
            if (!hasRequest)
                return NotFound();

            appointment.DentistId = targetDentist.Id;
            appointment.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            var patientName = $"{appointment.Patient.FirstName} {appointment.Patient.LastName}";
            await _audit.LogAsync(
                "Approve Patient Transfer",
                "Medical Records",
                $"[approved-transfer:{appointment.Id}][from-dentist:{sourceDentist.Id}][to-dentist:{targetDentist.Id}] Approved transfer of patient {patientName} to Dr. {targetDentist.FirstName} {targetDentist.LastName}.");

            TempData["TransferSuccess"] = $"{patientName} was transferred to Dr. {targetDentist.FirstName} {targetDentist.LastName}.";
            return RedirectToAction(nameof(TransferRequests));
        }

        private static int? ExtractMarkerId(string description, string marker)
        {
            var match = System.Text.RegularExpressions.Regex.Match(description, $@"\[{marker}:(\d+)\]");
            return match.Success && int.TryParse(match.Groups[1].Value, out var id) ? id : null;
        }

        public async Task<IActionResult> ViewPatientRecord(int id)
        {
            var record = await GetDentistRecordQuery().FirstOrDefaultAsync(a => a.Id == id);
            if (record == null)
                return NotFound();

            var model = new DentalSync.ViewModels.DentistPatientRecordItemViewModel
            {
                AppointmentId = record.Id,
                PatientId = record.PatientId,
                PatientName = $"{record.Patient.FirstName} {record.Patient.LastName}",
                DentistName = $"Dr. {record.Dentist.FirstName} {record.Dentist.LastName}",
                ServiceName = record.Service.Name,
                AppointmentDate = record.AppointmentDate,
                StartTime = record.StartTime,
                EndTime = record.EndTime,
                Status = record.Status,
                Notes = record.Notes
            };

            return View("~/Views/Dentist/ViewPatientRecord.cshtml", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ArchivePatientRecord(int id)
        {
            var record = await GetDentistRecordQuery().FirstOrDefaultAsync(a => a.Id == id);
            if (record == null)
                return NotFound();

            record.Status = "Archived";
            record.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            await _audit.LogAsync("Archive Patient Record", "Medical Records",
                $"Dentist archived patient record for {record.Patient.FirstName} {record.Patient.LastName}");

            TempData["PatientRecordSuccess"] = "Patient record archived successfully.";
            return RedirectToAction(nameof(ViewPatientRecords));
        }

        private IQueryable<Appointment> GetDentistRecordQuery()
        {
            var dentistUserId = _userManager.GetUserId(User);
            return _context.Appointments
                .Where(a => a.Dentist.UserId == dentistUserId && a.Status != "Archived")
                .Include(a => a.Patient)
                .Include(a => a.Dentist)
                .Include(a => a.Service);
        }

        public async Task<IActionResult> ViewAppointments(string search = "", string statusFilter = "", int page = 1)
        {
            var user = await _userManager.GetUserAsync(User);
            var dentist = await _context.Dentists.FirstOrDefaultAsync(d => d.UserId == user!.Id);

            const int pageSize = 5;
            var vm = new DentalSync.ViewModels.DentistViewAppointmentsViewModel
            {
                Search = search,
                StatusFilter = statusFilter,
                Page = Math.Max(1, page),
                PageSize = pageSize
            };

            if (dentist != null)
            {
                var query = _context.Appointments
                    .Where(a => a.DentistId == dentist.Id)
                    .Include(a => a.Patient)
                    .Include(a => a.Service)
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(search))
                {
                    var term = search.Trim();
                    query = query.Where(a =>
                        EF.Functions.Like(a.Patient.FirstName, $"%{term}%") ||
                        EF.Functions.Like(a.Patient.LastName, $"%{term}%") ||
                        EF.Functions.Like(a.Patient.ContactNumber, $"%{term}%") ||
                        EF.Functions.Like(a.Service.Name, $"%{term}%"));
                }

                if (!string.IsNullOrWhiteSpace(statusFilter))
                    query = query.Where(a => a.Status == statusFilter);

                vm.TotalAppointments = await query.CountAsync();
                vm.Appointments = await query
                    .OrderByDescending(a => a.AppointmentDate)
                    .ThenByDescending(a => a.StartTime)
                    .Skip((vm.Page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(a => new DentalSync.ViewModels.AppointmentListItemViewModel
                    {
                        Id = a.Id,
                        PatientId = a.PatientId,
                        PatientName = a.Patient.FirstName + " " + a.Patient.LastName,
                        DentistId = a.DentistId,
                        DentistName = "Dr. " + dentist.FirstName + " " + dentist.LastName,
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

            return View("~/Views/Dentist/ViewAppointments.cshtml", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateAppointmentStatus(int id, string status)
        {
            var appointment = await _context.Appointments
                .Include(a => a.Patient)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (appointment == null)
                return NotFound();

            appointment.Status = status;
            appointment.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            var pName = appointment.Patient != null
                ? $"{appointment.Patient.FirstName} {appointment.Patient.LastName}"
                : $"Appointment #{id}";
            await _audit.LogAsync("Update Appointment Status", "Appointments",
                $"Dentist updated appointment for {pName} status to '{status}'");

            TempData["AppointmentSuccess"] = $"Appointment status updated to '{status}'!";
            return RedirectToAction(nameof(ViewAppointments));
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

    }
}
