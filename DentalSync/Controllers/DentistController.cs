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
                .Where(log => log.Action == "Request Patient Transfer")
                .OrderByDescending(log => log.DateTime)
                .ToListAsync();

            var approvalLogs = await _context.AuditLogs
                .AsNoTracking()
                .Where(log => log.Action == "Approve Patient Transfer")
                .OrderByDescending(log => log.DateTime)
                .ToListAsync();

            var cancelLogs = await _context.AuditLogs
                .AsNoTracking()
                .Where(log => log.Action == "Cancel Patient Transfer")
                .OrderByDescending(log => log.DateTime)
                .ToListAsync();

            var pendingAppointmentIds = requestLogs
                .Select(log => new
                {
                    AppointmentId = ExtractMarkerId(log.Description, "appointment"),
                    SourceDentistId = ExtractMarkerId(log.Description, "source-dentist"),
                    TargetDentistId = ExtractMarkerId(log.Description, "target-dentist")
                })
                .Where(x => x.AppointmentId.HasValue && x.SourceDentistId.HasValue && x.TargetDentistId.HasValue)
                .Where(x => x.SourceDentistId.Value == dentist.Id)
                .Where(x => !approvalLogs.Any(log => ExtractMarkerId(log.Description, "approved-transfer") == x.AppointmentId.Value))
                .Where(x => !cancelLogs.Any(log => ExtractMarkerId(log.Description, "cancel-transfer") == x.AppointmentId.Value))
                .Select(x => x.AppointmentId!.Value)
                .Distinct()
                .ToHashSet();

            var pendingAppointments = await _context.Appointments
                .AsNoTracking()
                .Include(a => a.Patient)
                .Include(a => a.Service)
                .Where(a => a.DentistId == dentist.Id && a.Status == "Pending Dentist Approval")
                .Where(a => pendingAppointmentIds.Contains(a.Id) || a.Status == "Pending Dentist Approval")
                .OrderByDescending(a => a.UpdatedAt ?? a.CreatedAt)
                .ToListAsync();

            var pendingAppointmentSet = pendingAppointments
                .Select(a => a.Id)
                .ToHashSet();

            foreach (var requestLog in requestLogs)
            {
                var appointmentId = ExtractMarkerId(requestLog.Description, "appointment");
                var sourceDentistId = ExtractMarkerId(requestLog.Description, "source-dentist");
                if (!appointmentId.HasValue || !sourceDentistId.HasValue || sourceDentistId.Value != dentist.Id)
                    continue;

                if (approvalLogs.Any(log => ExtractMarkerId(log.Description, "approved-transfer") == appointmentId.Value))
                    continue;

                if (cancelLogs.Any(log => ExtractMarkerId(log.Description, "cancel-transfer") == appointmentId.Value))
                    continue;

                pendingAppointmentSet.Add(appointmentId.Value);
            }

            var finalizedPendingAppointments = await _context.Appointments
                .AsNoTracking()
                .Include(a => a.Patient)
                .Include(a => a.Service)
                .Where(a => pendingAppointmentSet.Contains(a.Id))
                .OrderByDescending(a => a.UpdatedAt ?? a.CreatedAt)
                .ToListAsync();

            pendingAppointments = finalizedPendingAppointments;

            model.Requests = pendingAppointments
                .Select(appointment =>
                {
                    var matchingLog = requestLogs
                        .Where(log => log.Description.Contains($"[appointment:{appointment.Id}]"))
                        .OrderByDescending(log => log.DateTime)
                        .FirstOrDefault();

                    var targetDentistId = matchingLog == null
                        ? 0
                        : ExtractMarkerId(matchingLog.Description, "target-dentist") ?? 0;

                    return new DentalSync.ViewModels.DentistTransferRequestItemViewModel
                    {
                        AppointmentId = appointment.Id,
                        PatientId = appointment.PatientId,
                        TargetDentistId = targetDentistId,
                        PatientName = $"{appointment.Patient.FirstName} {appointment.Patient.LastName}",
                        ServiceName = appointment.Service.Name,
                        AppointmentDate = appointment.AppointmentDate,
                        StartTime = appointment.StartTime,
                        RequestedAt = appointment.UpdatedAt ?? appointment.CreatedAt
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
        public async Task<IActionResult> ApprovePatientTransfer(int appointmentId, int targetDentistId = 0)
        {
            var user = await _userManager.GetUserAsync(User);
            var sourceDentist = user == null
                ? null
                : await _context.Dentists.FirstOrDefaultAsync(d => d.UserId == user.Id);

            if (sourceDentist == null)
            {
                TempData["TransferError"] = "Your dentist profile could not be found.";
                return RedirectToAction(nameof(TransferRequests));
            }

            var pendingRequestLog = await _context.AuditLogs
                .AsNoTracking()
                .Where(log => log.Action == "Request Patient Transfer" && log.Description.Contains($"[appointment:{appointmentId}]"))
                .OrderByDescending(log => log.DateTime)
                .FirstOrDefaultAsync();

            if (targetDentistId <= 0 && pendingRequestLog != null)
            {
                targetDentistId = ExtractMarkerId(pendingRequestLog.Description, "target-dentist") ?? 0;
            }

            var appointment = await _context.Appointments
                .Include(a => a.Patient)
                .Include(a => a.Service)
                .FirstOrDefaultAsync(a => a.Id == appointmentId);

            if (appointment == null)
            {
                TempData["TransferError"] = "The transfer request could not be found.";
                return RedirectToAction(nameof(TransferRequests));
            }

            var requestSourceDentistId = pendingRequestLog == null
                ? appointment.DentistId
                : ExtractMarkerId(pendingRequestLog.Description, "source-dentist") ?? appointment.DentistId;

            var targetDentist = await _context.Dentists
                .FirstOrDefaultAsync(d => d.Id == targetDentistId && d.Status == "Active");

            var hasRequest = pendingRequestLog != null &&
                (requestSourceDentistId == sourceDentist.Id || appointment.DentistId == sourceDentist.Id);

            if (!hasRequest || targetDentist == null || targetDentist.Id == sourceDentist.Id)
            {
                TempData["TransferError"] = "This transfer request is no longer valid for approval.";
                return RedirectToAction(nameof(TransferRequests));
            }

            appointment.DentistId = targetDentist.Id;
            appointment.Status = "Scheduled";
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

        private static List<int> ExtractExtraServiceIds(string? notes, int primaryServiceId)
        {
            if (string.IsNullOrWhiteSpace(notes))
                return new List<int>();

            var match = System.Text.RegularExpressions.Regex.Match(notes, @"\[svc:([\d,]+)\]");
            if (!match.Success)
                return new List<int>();

            return match.Groups[1].Value
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(x => int.TryParse(x, out var id) ? id : 0)
                .Where(id => id > 0 && id != primaryServiceId)
                .Distinct()
                .ToList();
        }

        private static string CleanNotesText(string? notes)
        {
            if (string.IsNullOrWhiteSpace(notes))
                return string.Empty;

            var cleaned = notes.Trim();
            cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"^\[svc:\d+(?:,\d+)*\]\s*", string.Empty, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            return cleaned.Trim();
        }

        public async Task<IActionResult> ViewPatientRecord(int id)
        {
            var record = await GetDentistRecordQuery().FirstOrDefaultAsync(a => a.Id == id);
            if (record == null)
                return NotFound();

            var extraServiceIds = ExtractExtraServiceIds(record.Notes, record.ServiceId);
            var serviceMap = await _context.Services
                .Where(s => s.Id == record.ServiceId || extraServiceIds.Contains(s.Id))
                .ToDictionaryAsync(s => s.Id, s => s);

            var treatmentNames = new List<string>();
            if (serviceMap.TryGetValue(record.ServiceId, out var primaryService))
                treatmentNames.Add(primaryService.Name);
            else
                treatmentNames.Add(record.Service.Name);

            foreach (var extraId in extraServiceIds)
            {
                if (serviceMap.TryGetValue(extraId, out var extraService) && !treatmentNames.Contains(extraService.Name))
                    treatmentNames.Add(extraService.Name);
            }

            var model = new DentalSync.ViewModels.DentistPatientRecordItemViewModel
            {
                AppointmentId = record.Id,
                PatientId = record.PatientId,
                PatientName = $"{record.Patient.FirstName} {record.Patient.LastName}",
                DentistName = $"Dr. {record.Dentist.FirstName} {record.Dentist.LastName}",
                ServiceName = record.Service.Name,
                ServiceCategory = record.Service.Category ?? "General",
                TreatmentNames = string.Join(", ", treatmentNames),
                AppointmentDate = record.AppointmentDate,
                StartTime = record.StartTime,
                EndTime = record.EndTime,
                Status = record.Status,
                Notes = CleanNotesText(record.Notes)
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
            var dentist = user == null
                ? null
                : await _context.Dentists.FirstOrDefaultAsync(d => d.UserId == user.Id);

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
                var rawAppointments = await query
                    .OrderByDescending(a => a.AppointmentDate)
                    .ThenByDescending(a => a.StartTime)
                    .Skip((vm.Page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var allExtraServiceIds = rawAppointments
                    .SelectMany(a => ExtractExtraServiceIds(a.Notes, a.ServiceId))
                    .Distinct()
                    .ToList();

                var allServiceIds = rawAppointments.Select(a => a.ServiceId).Concat(allExtraServiceIds).Distinct().ToList();
                var serviceMap = await _context.Services
                    .Where(s => allServiceIds.Contains(s.Id))
                    .ToDictionaryAsync(s => s.Id, s => s);

                vm.Appointments = rawAppointments.Select(a =>
                {
                    var extraIds = ExtractExtraServiceIds(a.Notes, a.ServiceId);
                    var cleanNotes = CleanNotesText(a.Notes);

                    var primaryName = serviceMap.TryGetValue(a.ServiceId, out var ps) ? ps.Name : (a.Service?.Name ?? "General Service");
                    var primaryCost = ps?.Cost ?? a.Service?.Cost ?? 0m;

                    decimal extraCost = 0m;
                    var extraNames = new List<string>();
                    foreach (var eid in extraIds)
                    {
                        if (serviceMap.TryGetValue(eid, out var es))
                        {
                            extraNames.Add(es.Name);
                            extraCost += es.Cost;
                        }
                    }

                    var allNames = new[] { primaryName }.Concat(extraNames);

                    return new DentalSync.ViewModels.AppointmentListItemViewModel
                    {
                        Id = a.Id,
                        PatientId = a.PatientId,
                        PatientName = $"{a.Patient?.FirstName} {a.Patient?.LastName}",
                        DentistId = a.DentistId,
                        DentistName = $"Dr. {dentist.FirstName} {dentist.LastName}",
                        ServiceId = a.ServiceId,
                        ServiceName = primaryName,
                        ServiceNames = string.Join(", ", allNames),
                        TotalCost = primaryCost + extraCost,
                        AppointmentDate = a.AppointmentDate,
                        StartTime = a.StartTime,
                        EndTime = a.EndTime,
                        Status = a.Status,
                        Notes = cleanNotes
                    };
                }).ToList();
            }

            return View("~/Views/Dentist/ViewAppointments.cshtml", vm);
        }

        [HttpGet]
        public async Task<IActionResult> GetAppointmentDetails(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            var dentist = user == null
                ? null
                : await _context.Dentists.FirstOrDefaultAsync(d => d.UserId == user.Id);

            if (dentist == null)
                return Json(new { success = false, message = "Dentist profile not found." });

            var appointment = await _context.Appointments
                .Include(a => a.Patient)
                .Include(a => a.Dentist)
                .Include(a => a.Service)
                .FirstOrDefaultAsync(a => a.Id == id && a.DentistId == dentist.Id);

            if (appointment == null)
                return Json(new { success = false, message = "Appointment not found." });

            var patient = appointment.Patient;

            // Extract all services
            var extraServiceIds = ExtractExtraServiceIds(appointment.Notes, appointment.ServiceId);
            var allServiceIds = new List<int> { appointment.ServiceId }.Concat(extraServiceIds).Distinct().ToList();

            var servicesList = await _context.Services
                .Where(s => allServiceIds.Contains(s.Id))
                .ToListAsync();

            var serviceDetails = new List<object>();
            decimal totalServicesCost = 0m;

            // Primary service
            var primary = servicesList.FirstOrDefault(s => s.Id == appointment.ServiceId) ?? appointment.Service;
            if (primary != null)
            {
                serviceDetails.Add(new
                {
                    id = primary.Id,
                    name = primary.Name,
                    category = primary.Category ?? "General",
                    cost = primary.Cost,
                    isPrimary = true
                });
                totalServicesCost += primary.Cost;
            }

            // Extra services
            foreach (var extraId in extraServiceIds)
            {
                var extra = servicesList.FirstOrDefault(s => s.Id == extraId);
                if (extra != null)
                {
                    serviceDetails.Add(new
                    {
                        id = extra.Id,
                        name = extra.Name,
                        category = extra.Category ?? "General",
                        cost = extra.Cost,
                        isPrimary = false
                    });
                    totalServicesCost += extra.Cost;
                }
            }

            // Patient Invoices & Billing
            var rawInvoices = await _context.Invoices
                .Where(i => i.PatientId == appointment.PatientId)
                .Include(i => i.InvoiceItems)
                .Include(i => i.Payments)
                .OrderByDescending(i => i.InvoiceDate)
                .ToListAsync();

            var invoiceList = rawInvoices.Select(inv => new
            {
                id = inv.Id,
                invoiceNumber = inv.InvoiceNumber,
                invoiceDate = inv.InvoiceDate.ToString("MMMM dd, yyyy"),
                subtotal = inv.Subtotal,
                discount = inv.Discount ?? 0,
                totalAmount = inv.TotalAmount,
                amountPaid = inv.Payments.Sum(p => p.Amount),
                balance = inv.TotalAmount - inv.Payments.Sum(p => p.Amount),
                status = inv.Status,
                items = inv.InvoiceItems.Select(item => new
                {
                    description = item.Description ?? "Dental Service",
                    quantity = item.Quantity,
                    unitPrice = item.UnitPrice,
                    amount = item.Amount
                }).ToList()
            }).ToList();

            decimal totalBilled = invoiceList.Sum(i => i.totalAmount);
            decimal totalPaid = invoiceList.Sum(i => i.amountPaid);
            decimal totalBalance = invoiceList.Sum(i => i.balance);

            // Calculate Patient Age
            int? age = null;
            if (patient?.DateOfBirth.HasValue == true)
            {
                var dob = patient.DateOfBirth.Value;
                var today = DateOnly.FromDateTime(DateTime.Today);
                var calculatedAge = today.Year - dob.Year;
                if (dob > today.AddYears(-calculatedAge)) calculatedAge--;
                age = calculatedAge;
            }

            var patientName = patient != null ? $"{patient.FirstName} {patient.LastName}" : "Unknown Patient";
            var initials = patient != null && !string.IsNullOrWhiteSpace(patient.FirstName) && !string.IsNullOrWhiteSpace(patient.LastName)
                ? $"{(patient.FirstName.Length > 0 ? patient.FirstName[..1] : "")}{(patient.LastName.Length > 0 ? patient.LastName[..1] : "")}".ToUpper() 
                : "PT";

            return Json(new
            {
                success = true,
                appointment = new
                {
                    id = appointment.Id,
                    date = appointment.AppointmentDate.ToString("MMMM dd, yyyy"),
                    time = appointment.StartTime.ToString("hh:mm tt"),
                    endTime = appointment.EndTime.HasValue ? appointment.EndTime.Value.ToString("hh:mm tt") : null,
                    status = appointment.Status,
                    notes = CleanNotesText(appointment.Notes),
                    createdAt = appointment.CreatedAt.ToString("MMMM dd, yyyy hh:mm tt")
                },
                patient = new
                {
                    id = patient?.Id ?? 0,
                    fullName = patientName,
                    initials = initials,
                    contactNumber = string.IsNullOrWhiteSpace(patient?.ContactNumber) ? "N/A" : patient.ContactNumber,
                    email = string.IsNullOrWhiteSpace(patient?.Email) ? "N/A" : patient.Email,
                    gender = string.IsNullOrWhiteSpace(patient?.Gender) ? "Not Specified" : patient.Gender,
                    dateOfBirth = patient?.DateOfBirth.HasValue == true ? patient.DateOfBirth.Value.ToString("MMMM dd, yyyy") : "N/A",
                    age = age.HasValue ? $"{age} yrs old" : "N/A",
                    address = string.IsNullOrWhiteSpace(patient?.Address) ? "N/A" : patient.Address,
                    emergencyContact = string.IsNullOrWhiteSpace(patient?.EmergencyContact) ? "N/A" : patient.EmergencyContact,
                    emergencyPhone = string.IsNullOrWhiteSpace(patient?.EmergencyPhone) ? "N/A" : patient.EmergencyPhone,
                    medicalNotes = string.IsNullOrWhiteSpace(patient?.MedicalNotes) ? "No medical alerts or special conditions recorded." : patient.MedicalNotes
                },
                dentist = new
                {
                    id = dentist.Id,
                    name = $"Dr. {dentist.FirstName} {dentist.LastName}",
                    specialization = dentist.Specialization
                },
                services = serviceDetails,
                totalServicesCost = totalServicesCost,
                billing = new
                {
                    totalBilled = totalBilled,
                    totalPaid = totalPaid,
                    totalBalance = totalBalance,
                    invoices = invoiceList
                }
            });
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
