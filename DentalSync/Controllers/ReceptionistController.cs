using DentalSync.Data;
using DentalSync.Models;
using DentalSync.Services;
using DentalSync.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace DentalSync.Controllers
{
    [Authorize(Roles = "Receptionist")]
    public class ReceptionistController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<Users> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly AuditService _audit;
        private readonly InventoryDeductionService _deductionService;
        private readonly IDentistAvailabilityService _availabilityService;

        public ReceptionistController(
            AppDbContext context,
            UserManager<Users> userManager,
            RoleManager<IdentityRole> roleManager,
            AuditService audit,
            InventoryDeductionService deductionService,
            IDentistAvailabilityService availabilityService)
        {
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
            _audit = audit;
            _deductionService = deductionService;
            _availabilityService = availabilityService;
        }

        public async Task<IActionResult> Receptionist_Dashboard()
        {
            var user = await _userManager.GetUserAsync(User);
            ViewBag.UserFullName = user?.FullName ?? user?.UserName ?? "there";

            var today = DateOnly.FromDateTime(DateTime.Today);
            var totalPatients = await _context.Patients.CountAsync();
            var todayAppointmentsCount = await _context.Appointments.CountAsync(a => a.AppointmentDate == today);
            var totalAppointmentsCount = await _context.Appointments.CountAsync();
            var totalRevenue = await _context.Payments.SumAsync(p => (decimal?)p.Amount) ?? 0m;

            var openInvoices = await _context.Invoices
                .Where(i => i.Status == "Pending" || i.Status == "PartiallyPaid")
                .Include(i => i.Payments)
                .ToListAsync();

            decimal pendingPayments = 0m;
            foreach (var invoice in openInvoices)
            {
                var paid = invoice.Payments.Sum(p => p.Amount);
                pendingPayments += Math.Max(0, invoice.TotalAmount - paid);
            }

            var appointments = await _context.Appointments
                .Include(a => a.Patient)
                .Include(a => a.Dentist)
                .Include(a => a.Service)
                .Where(a => a.AppointmentDate == today)
                .OrderBy(a => a.StartTime)
                .Take(4)
                .ToListAsync();

            if (!appointments.Any())
            {
                appointments = await _context.Appointments
                    .Include(a => a.Patient)
                    .Include(a => a.Dentist)
                    .Include(a => a.Service)
                    .OrderByDescending(a => a.AppointmentDate)
                    .ThenBy(a => a.StartTime)
                    .Take(4)
                    .ToListAsync();
            }

            var todayAppointmentItems = appointments.Select(appointment =>
            {
                var patientName = $"{appointment.Patient?.FirstName} {appointment.Patient?.LastName}".Trim();
                if (string.IsNullOrWhiteSpace(patientName)) patientName = "Patient";

                var dentistName = $"{appointment.Dentist?.FirstName} {appointment.Dentist?.LastName}".Trim();
                if (string.IsNullOrWhiteSpace(dentistName)) dentistName = "Staff";

                return new DashboardAppointmentItemViewModel
                {
                    Id = appointment.Id,
                    PatientName = patientName,
                    Initials = GetInitials(patientName),
                    ServiceName = appointment.Service?.Name ?? "General Checkup",
                    DentistName = dentistName.StartsWith("Dr.") ? dentistName : $"Dr. {dentistName}",
                    AppointmentDate = appointment.AppointmentDate,
                    StartTime = appointment.StartTime,
                    EndTime = appointment.EndTime,
                    CreatedAt = appointment.CreatedAt,
                    Status = appointment.Status
                };
            }).ToList();

            var recentActivities = await _context.AuditLogs
                .OrderByDescending(log => log.DateTime)
                .Take(4)
                .Select(log => new DashboardActivityItemViewModel
                {
                    Id = log.Id,
                    User = log.User,
                    Action = log.Action,
                    Module = log.Module,
                    Description = log.Description,
                    Timestamp = log.DateTime,
                    RelativeTimeText = ""
                })
                .ToListAsync();

            foreach (var activity in recentActivities)
            {
                activity.RelativeTimeText = GetRelativeTimeString(activity.Timestamp);
            }

            var model = new AdminDashboardViewModel
            {
                TotalPatients = totalPatients,
                TodayAppointmentsCount = todayAppointmentsCount,
                TotalAppointmentsCount = totalAppointmentsCount,
                TotalRevenue = totalRevenue,
                PendingPayments = pendingPayments,
                TodayAppointments = todayAppointmentItems,
                RecentActivities = recentActivities
            };

            return View("~/Views/Receptionists/Receptionist_Dashboard.cshtml", model);
        }

        private static string GetInitials(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "P";
            var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1) return parts[0][..1].ToUpper();
            return $"{parts[0][0]}{parts[^1][0]}".ToUpper();
        }

        private static string GetRelativeTimeString(DateTime dateTime)
        {
            var timeSpan = DateTime.Now - dateTime;
            if (timeSpan.TotalMinutes < 1) return "Just now";
            if (timeSpan.TotalMinutes < 60) return $"{(int)timeSpan.TotalMinutes} min ago";
            if (timeSpan.TotalHours < 24) return $"{(int)timeSpan.TotalHours} hr{((int)timeSpan.TotalHours > 1 ? "s" : "")} ago";
            if (timeSpan.TotalDays < 7) return $"{(int)timeSpan.TotalDays} day{((int)timeSpan.TotalDays > 1 ? "s" : "")} ago";
            return dateTime.ToString("MMM dd, h:mm tt");
        }

        public async Task<IActionResult> Register_Patients(string search = "", int page = 1)
        {
            const int pageSize = 4;
            var query = _context.Patients.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(p =>
                    EF.Functions.Like(p.FirstName, $"%{term}%") ||
                    EF.Functions.Like(p.LastName, $"%{term}%") ||
                    (p.MiddleName != null && EF.Functions.Like(p.MiddleName, $"%{term}%")) ||
                    EF.Functions.Like(p.ContactNumber, $"%{term}%") ||
                    EF.Functions.Like(p.Address, $"%{term}%"));
            }

            var totalPatients = await query.CountAsync();
            var currentPage = Math.Max(1, page);

            var rawPatients = await query
                .OrderByDescending(p => p.CreatedAt)
                .Skip((currentPage - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var patientRows = rawPatients.Select(p =>
            {
                var nameParts = new List<string> { p.FirstName };
                if (!string.IsNullOrWhiteSpace(p.MiddleName)) nameParts.Add(p.MiddleName);
                nameParts.Add(p.LastName);
                if (!string.IsNullOrWhiteSpace(p.Suffix)) nameParts.Add(p.Suffix);

                return new PatientListItemViewModel
                {
                    Id = p.Id,
                    FirstName = p.FirstName,
                    LastName = p.LastName,
                    MiddleName = p.MiddleName,
                    Suffix = p.Suffix,
                    FullName = string.Join(" ", nameParts),
                    ContactNumber = p.ContactNumber,
                    Address = p.Address,
                    CreatedAt = p.CreatedAt
                };
            }).ToList();

            var model = new PatientManagementViewModel
            {
                Search = search,
                Page = currentPage,
                PageSize = pageSize,
                TotalPatients = totalPatients,
                Patients = patientRows
            };

            return View("~/Views/Receptionists/Register_Patients.cshtml", model);
        }

        public async Task<IActionResult> PatientRecords(string search = "", string statusFilter = "", int page = 1)
        {
            const int pageSize = 6;
            var query = _context.Appointments
                .AsNoTracking()
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
                    (a.Patient.MiddleName != null && EF.Functions.Like(a.Patient.MiddleName, $"%{term}%")) ||
                    EF.Functions.Like(a.Patient.ContactNumber, $"%{term}%") ||
                    EF.Functions.Like(a.Dentist.FirstName, $"%{term}%") ||
                    EF.Functions.Like(a.Dentist.LastName, $"%{term}%"));
            }

            if (!string.IsNullOrWhiteSpace(statusFilter))
            {
                query = query.Where(a => a.Status == statusFilter);
            }

            var totalRecords = await query.CountAsync();
            var currentPage = Math.Max(1, page);
            var records = await query
                .OrderByDescending(a => a.AppointmentDate)
                .ThenByDescending(a => a.StartTime)
                .Skip((currentPage - 1) * pageSize)
                .Take(pageSize)
                .Select(a => new DentistPatientRecordItemViewModel
                {
                    AppointmentId = a.Id,
                    PatientId = a.PatientId,
                    CurrentDentistId = a.DentistId,
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

            var recordIds = records.Select(record => record.AppointmentId).ToHashSet();
            var requestedIds = await _context.AuditLogs
                .AsNoTracking()
                .Where(log => log.Action == "Request Patient Transfer")
                .Select(log => log.Description)
                .ToListAsync();
            var approvedIds = await _context.AuditLogs
                .AsNoTracking()
                .Where(log => log.Action == "Approve Patient Transfer")
                .Select(log => log.Description)
                .ToListAsync();
            var cancelledIds = await _context.AuditLogs
                .AsNoTracking()
                .Where(log => log.Action == "Cancel Patient Transfer")
                .Select(log => log.Description)
                .ToListAsync();
            var approvedAppointmentIds = approvedIds
                .Select(description => ExtractMarkerId(description, "approved-transfer"))
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .ToHashSet();
            var cancelledAppointmentIds = cancelledIds
                .Select(description => ExtractMarkerId(description, "cancel-transfer"))
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .ToHashSet();
            var pendingAppointmentIds = requestedIds
                .Select(description => ExtractMarkerId(description, "appointment"))
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .Where(recordIds.Contains)
                .Where(id => !approvedAppointmentIds.Contains(id))
                .Where(id => !cancelledAppointmentIds.Contains(id))
                .ToHashSet();

            foreach (var record in records)
            {
                record.TransferRequested = pendingAppointmentIds.Contains(record.AppointmentId)
                    || string.Equals(record.Status, "Pending Dentist Approval", StringComparison.OrdinalIgnoreCase);

                record.AvailableDentists = await _availabilityService.GetDentistAvailabilityOptionsAsync(
                    record.AppointmentDate,
                    record.StartTime,
                    record.EndTime,
                    record.CurrentDentistId,
                    record.AppointmentId);
            }

            var activeDentists = await _context.Dentists
                .AsNoTracking()
                .Where(d => d.Status == "Active")
                .OrderBy(d => d.LastName)
                .ThenBy(d => d.FirstName)
                .ToListAsync();

            var model = new ReceptionistPatientRecordsViewModel
            {
                Search = search,
                StatusFilter = statusFilter,
                Page = currentPage,
                PageSize = pageSize,
                TotalRecords = totalRecords,
                Records = records,
                Dentists = activeDentists
            };

            return View("~/Views/Receptionists/PatientRecords.cshtml", model);
        }

        private static int? ExtractMarkerId(string description, string marker)
        {
            var match = System.Text.RegularExpressions.Regex.Match(description, $@"\[{marker}:(\d+)\]");
            return match.Success && int.TryParse(match.Groups[1].Value, out var id) ? id : null;
        }

        private static string? ExtractMarkerText(string description, string marker)
        {
            var match = System.Text.RegularExpressions.Regex.Match(description, $@"\[{marker}:([^\]]+)\]");
            return match.Success ? match.Groups[1].Value : null;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestPatientTransfer(int id, int targetDentistId, string search = "", string statusFilter = "", int page = 1)
        {
            var appointment = await _context.Appointments
                .Include(a => a.Patient)
                .Include(a => a.Dentist)
                .FirstOrDefaultAsync(a => a.Id == id);
            if (appointment == null)
                return NotFound();

            if (targetDentistId <= 0)
            {
                TempData["PatientRecordError"] = "Please select a dentist to transfer the patient to.";
                return RedirectToAction(nameof(PatientRecords), new { search, statusFilter, page });
            }

            var targetDentist = await _context.Dentists
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == targetDentistId && d.Status == "Active");

            if (targetDentist == null || targetDentistId == appointment.DentistId)
            {
                TempData["PatientRecordError"] = "Please choose a valid dentist other than the current one.";
                return RedirectToAction(nameof(PatientRecords), new { search, statusFilter, page });
            }

            var isAvailable = await _availabilityService.IsDentistAvailableAsync(
                targetDentistId,
                appointment.AppointmentDate,
                appointment.StartTime,
                appointment.EndTime,
                appointment.Id);

            if (!isAvailable)
            {
                TempData["PatientRecordError"] = "Selected dentist is not available on this appointment date.";
                return RedirectToAction(nameof(PatientRecords), new { search, statusFilter, page });
            }

            var latestTransferAction = await _context.AuditLogs
                .AsNoTracking()
                .Where(log =>
                    log.Description.Contains($"[appointment:{appointment.Id}]") &&
                    (log.Action == "Request Patient Transfer" ||
                     log.Action == "Approve Patient Transfer" ||
                     log.Action == "Cancel Patient Transfer"))
                .OrderByDescending(log => log.DateTime)
                .Select(log => log.Action)
                .FirstOrDefaultAsync();

            var isAlreadyPending = string.Equals(appointment.Status, "Pending Dentist Approval", StringComparison.OrdinalIgnoreCase)
                || latestTransferAction == "Request Patient Transfer";

            if (isAlreadyPending)
            {
                TempData["PatientRecordSuccess"] = "A transfer request is already pending for this patient record.";
                return RedirectToAction(nameof(PatientRecords), new { search, statusFilter, page });
            }

            var previousStatus = appointment.Status;
            var patientName = $"{appointment.Patient.FirstName} {appointment.Patient.LastName}";
            appointment.Status = "Pending Dentist Approval";
            appointment.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                "Request Patient Transfer",
                "Medical Records",
                $"[transfer-request][appointment:{appointment.Id}][source-dentist:{appointment.DentistId}][target-dentist:{targetDentist.Id}][previous-status:{previousStatus}] Receptionist requested to transfer patient {patientName} to Dr. {targetDentist.FirstName} {targetDentist.LastName} from Dr. {appointment.Dentist.FirstName} {appointment.Dentist.LastName}." );

            TempData["PatientRecordSuccess"] = "Transfer request sent to the patient's current dentist.";
            return RedirectToAction(nameof(PatientRecords), new { search, statusFilter, page });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelPatientTransfer(int id, string search = "", string statusFilter = "", int page = 1)
        {
            var appointment = await _context.Appointments
                .Include(a => a.Patient)
                .Include(a => a.Dentist)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (appointment == null)
                return NotFound();

            var latestTransferLog = await _context.AuditLogs
                .AsNoTracking()
                .Where(log =>
                    log.Description.Contains($"[appointment:{id}]") &&
                    (log.Action == "Request Patient Transfer" ||
                     log.Action == "Approve Patient Transfer" ||
                     log.Action == "Cancel Patient Transfer"))
                .OrderByDescending(log => log.DateTime)
                .FirstOrDefaultAsync();

            var latestAction = latestTransferLog?.Action;

            if (latestAction == "Approve Patient Transfer")
            {
                TempData["PatientRecordError"] = "This transfer has already been approved and cannot be cancelled.";
                return RedirectToAction(nameof(PatientRecords), new { search, statusFilter, page });
            }

            if (latestAction != "Request Patient Transfer" && appointment.Status != "Pending Dentist Approval")
            {
                TempData["PatientRecordError"] = "There is no pending transfer to cancel for this patient.";
                return RedirectToAction(nameof(PatientRecords), new { search, statusFilter, page });
            }

            appointment.Status = "Cancelled";
            appointment.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                "Cancel Patient Transfer",
                "Medical Records",
                $"[cancel-transfer:{appointment.Id}][source-dentist:{appointment.DentistId}] Receptionist cancelled the pending transfer request for patient {appointment.Patient.FirstName} {appointment.Patient.LastName}." );

            TempData["PatientRecordSuccess"] = "Transfer request cancelled and the patient record is now marked as cancelled.";
            return RedirectToAction(nameof(PatientRecords), new { search, statusFilter, page });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreatePatient(CreatePatientViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["PatientCreateError"] = string.Join(" ", ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage));
                return RedirectToAction(nameof(Register_Patients));
            }

            var existingUser = await _userManager.FindByEmailAsync(model.Email.Trim());
            if (existingUser != null)
            {
                TempData["PatientCreateError"] = $"Email '{model.Email.Trim()}' is already in use.";
                return RedirectToAction(nameof(Register_Patients));
            }

            var user = new Users
            {
                FullName = $"{model.FirstName.Trim()} {model.LastName.Trim()}",
                UserName = model.Email.Trim(),
                Email = model.Email.Trim(),
                EmailConfirmed = true,
                LockoutEnabled = true
            };

            var createResult = await _userManager.CreateAsync(user, model.Password);
            if (!createResult.Succeeded)
            {
                TempData["PatientCreateError"] = string.Join(" ", createResult.Errors.Select(e => e.Description));
                return RedirectToAction(nameof(Register_Patients));
            }

            if (!await _roleManager.RoleExistsAsync("Patient"))
            {
                await _roleManager.CreateAsync(new IdentityRole("Patient"));
            }
            await _userManager.AddToRoleAsync(user, "Patient");

            var patient = new Patient
            {
                FirstName = model.FirstName.Trim(),
                LastName = model.LastName.Trim(),
                MiddleName = string.IsNullOrWhiteSpace(model.MiddleName) ? null : model.MiddleName.Trim(),
                Suffix = string.IsNullOrWhiteSpace(model.Suffix) ? null : model.Suffix.Trim(),
                ContactNumber = model.ContactNumber.Trim(),
                Address = model.Address.Trim(),
                Email = model.Email.Trim(),
                UserId = user.Id,
                CreatedAt = DateTime.UtcNow
            };

            _context.Patients.Add(patient);
            await _context.SaveChangesAsync();

            await _audit.LogAsync("Register Patient", "Patient Management", $"Registered patient {patient.FirstName} {patient.LastName} ({patient.Email})");

            TempData["PatientSuccess"] = "Patient registered successfully!";
            return RedirectToAction(nameof(Register_Patients));
        }

        // =================== Appointments ===================
        public async Task<IActionResult> ManageAppointments()
        {
            var dentistUsers = await _userManager.GetUsersInRoleAsync("Dentist");
            var dentistUserIds = dentistUsers.Select(u => u.Id).ToHashSet();

            foreach (var du in dentistUsers)
            {
                var exists = await _context.Dentists.AnyAsync(d => d.UserId == du.Id);
                if (!exists)
                {
                    var parts = (du.FullName ?? du.UserName ?? "Dentist User")
                                    .Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    _context.Dentists.Add(new Dentist
                    {
                        UserId         = du.Id,
                        FirstName      = parts.Length > 0 ? parts[0] : "Dentist",
                        LastName       = parts.Length > 1 ? string.Join(" ", parts.Skip(1)) : "User",
                        Email          = du.Email,
                        Specialization = "General Dentistry",
                        Status         = "Active",
                        CreatedAt      = DateTime.UtcNow
                    });
                }
            }
            await _context.SaveChangesAsync();

            var patientUsers = await _userManager.GetUsersInRoleAsync("Patient");
            var patientUserIds = patientUsers.Select(u => u.Id).ToHashSet();

            var rawAppointments = await _context.Appointments
                .Include(a => a.Patient)
                .Include(a => a.Dentist)
                .Include(a => a.Service)
                .OrderByDescending(a => a.AppointmentDate)
                .ThenByDescending(a => a.StartTime)
                .ToListAsync();

            var allServiceIds = rawAppointments.Select(a => a.ServiceId).Distinct().ToList();
            var serviceMap = await _context.Services
                .Where(s => allServiceIds.Contains(s.Id))
                .ToDictionaryAsync(s => s.Id, s => s);

            var appointments = rawAppointments.Select(a =>
            {
                // Parse extra service IDs stored in Notes as "[svc:1,2,3]..."
                var extraIds = new List<int>();
                var userNotes = a.Notes ?? string.Empty;
                var svcTag = System.Text.RegularExpressions.Regex.Match(userNotes, @"\[svc:([\d,]+)\]");
                if (svcTag.Success)
                {
                    extraIds = svcTag.Groups[1].Value.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(x => int.TryParse(x.Trim(), out var id) ? id : 0)
                        .Where(id => id > 0 && id != a.ServiceId)
                        .ToList();
                    userNotes = userNotes.Replace(svcTag.Value, string.Empty).Trim();
                }

                var primaryName = serviceMap.TryGetValue(a.ServiceId, out var ps) ? ps.Name : "General Service";
                var primaryCost = ps?.Cost ?? 0m;

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

                return new AppointmentListItemViewModel
                {
                    Id = a.Id,
                    PatientId = a.PatientId,
                    PatientName = $"{a.Patient?.FirstName} {a.Patient?.LastName}",
                    DentistId = a.DentistId,
                    DentistName = $"Dr. {a.Dentist?.FirstName} {a.Dentist?.LastName}",
                    ServiceId = a.ServiceId,
                    ServiceName = primaryName,
                    ServiceNames = string.Join(", ", allNames),
                    TotalCost = primaryCost + extraCost,
                    AppointmentDate = a.AppointmentDate,
                    StartTime = a.StartTime,
                    EndTime = a.EndTime,
                    Status = a.Status,
                    Notes = userNotes
                };
            }).ToList();

            var model = new ManageAppointmentsViewModel
            {
                Appointments = appointments,
                Patients = await _context.Patients
                    .Where(p => p.UserId != null && patientUserIds.Contains(p.UserId))
                    .OrderBy(p => p.LastName)
                    .ToListAsync(),
                Dentists = await _context.Dentists
                    .Where(d => d.Status == "Active" && d.UserId != null && dentistUserIds.Contains(d.UserId))
                    .OrderBy(d => d.LastName)
                    .ToListAsync(),
                Services = await _context.Services.Where(s => s.IsActive).OrderBy(s => s.Name).ToListAsync(),
                NewAppointment = new CreateAppointmentViewModel()
            };

            return View("~/Views/Receptionists/ManageAppointments.cshtml", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateAppointment([Bind(Prefix = "NewAppointment")] CreateAppointmentViewModel model)
        {
            // Remove model-state errors for the old single ServiceId — we use SelectedServiceIds now
            ModelState.Remove("NewAppointment.ServiceId");

            if (model.SelectedServiceIds == null || model.SelectedServiceIds.Count == 0)
            {
                TempData["AppointmentError"] = "Please select at least one dental service.";
                return RedirectToAction(nameof(ManageAppointments));
            }

            if (!ModelState.IsValid)
            {
                TempData["AppointmentError"] = "Failed to create appointment. Please fill in all fields.";
                return RedirectToAction(nameof(ManageAppointments));
            }

            // Primary service = first selection; extras stored as tag in Notes
            var primaryId = model.SelectedServiceIds[0];
            var extraIds  = model.SelectedServiceIds.Skip(1).ToList();

            string notesValue = model.Notes?.Trim() ?? string.Empty;
            if (extraIds.Count > 0)
                notesValue = $"[svc:{string.Join(",", extraIds)}] {notesValue}".Trim();

            var appointment = new Appointment
            {
                PatientId = model.PatientId,
                DentistId = model.DentistId,
                ServiceId = primaryId,
                AppointmentDate = model.AppointmentDate,
                StartTime = model.StartTime,
                Status = "Scheduled",
                Notes = notesValue,
                CreatedAt = DateTime.UtcNow
            };

            _context.Appointments.Add(appointment);
            await _context.SaveChangesAsync();

            var patient = await _context.Patients.FindAsync(model.PatientId);
            var pName = patient != null ? $"{patient.FirstName} {patient.LastName}" : $"Patient #{model.PatientId}";
            var serviceCount = model.SelectedServiceIds.Count;

            await _audit.LogAsync("Schedule Appointment", "Appointments",
                $"Scheduled appointment for {pName} on {model.AppointmentDate:yyyy-MM-dd} at {model.StartTime} ({serviceCount} service(s))");

            TempData["AppointmentSuccess"] = "Appointment scheduled successfully!";
            return RedirectToAction(nameof(ManageAppointments));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateAppointmentStatus(int id, string status)
        {
            var appointment = await _context.Appointments
                .Include(a => a.Patient)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (appointment == null)
            {
                return NotFound();
            }

            appointment.Status = status;
            appointment.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            var pName = appointment.Patient != null ? $"{appointment.Patient.FirstName} {appointment.Patient.LastName}" : $"Appointment #{id}";
            await _audit.LogAsync("Update Appointment Status", "Appointments", $"Updated appointment for {pName} status to '{status}'");

            TempData["AppointmentSuccess"] = $"Appointment status updated to '{status}'!";
            return RedirectToAction(nameof(ManageAppointments));
        }

        // =================== Bills & Payments ===================
        public async Task<IActionResult> BillsAndPayments()
        {
            var rawInvoices = await _context.Invoices
                .Include(i => i.Patient)
                .Include(i => i.InvoiceItems)
                .Include(i => i.Payments)
                .OrderByDescending(i => i.InvoiceDate)
                .ToListAsync();

            var invoices = rawInvoices.Select(i => new InvoiceListItemViewModel
            {
                Id = i.Id,
                InvoiceNumber = i.InvoiceNumber,
                PatientName = $"{i.Patient?.FirstName} {i.Patient?.LastName}",
                InvoiceDate = i.InvoiceDate,
                Subtotal = i.Subtotal,
                Discount = i.Discount ?? 0,
                TotalAmount = i.TotalAmount,
                Status = i.Status,
                AmountPaid = i.Payments.Sum(p => p.Amount),
                Items = i.InvoiceItems.Select(item => item.Description ?? "Dental Service").ToList()
            }).ToList();

            var model = new BillsAndPaymentsViewModel
            {
                Invoices = invoices,
                Patients = await _context.Patients.OrderBy(p => p.LastName).ToListAsync(),
                Services = await _context.Services.Where(s => s.IsActive).OrderBy(s => s.Name).ToListAsync(),
                NewInvoice = new CreateInvoiceViewModel(),
                NewPayment = new RecordPaymentViewModel()
            };

            return View("~/Views/Receptionists/BillsAndPayments.cshtml", model);
        }

        [HttpGet]
        public async Task<IActionResult> GetPatientServices(int patientId)
        {
            // Get all appointments for this patient and collect all primary + additional services
            var appointments = await _context.Appointments
                .Where(a => a.PatientId == patientId)
                .Include(a => a.Service)
                .OrderByDescending(a => a.AppointmentDate)
                .ThenByDescending(a => a.StartTime)
                .ToListAsync();

            if (appointments.Count == 0)
                return Json(new { services = Array.Empty<object>() });

            var allServiceIds = new List<int>();

            foreach (var a in appointments)
            {
                if (a.ServiceId > 0)
                {
                    allServiceIds.Add(a.ServiceId);
                }

                if (!string.IsNullOrWhiteSpace(a.Notes) && a.Notes.StartsWith("[svc:"))
                {
                    var closeBracket = a.Notes.IndexOf(']');
                    if (closeBracket > 5)
                    {
                        var rawIds = a.Notes.Substring(5, closeBracket - 5);
                        var parsedIds = rawIds.Split(',', StringSplitOptions.RemoveEmptyEntries)
                            .Select(idStr => int.TryParse(idStr, out var val) ? val : 0)
                            .Where(val => val > 0);
                        allServiceIds.AddRange(parsedIds);
                    }
                }
            }

            var distinctServiceIds = allServiceIds.Distinct().ToList();

            var services = await _context.Services
                .Where(s => distinctServiceIds.Contains(s.Id))
                .Select(s => new
                {
                    id   = s.Id,
                    name = s.Name,
                    cost = s.Cost
                })
                .ToListAsync();

            return Json(new { services });
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateInvoice([Bind(Prefix = "NewInvoice")] CreateInvoiceViewModel model)
        {
            if (!ModelState.IsValid || model.SelectedServiceIds.Count == 0)
            {
                TempData["InvoiceError"] = "Failed to create invoice. Select a patient and at least one service.";
                return RedirectToAction(nameof(BillsAndPayments));
            }

            var services = await _context.Services
                .Where(s => model.SelectedServiceIds.Contains(s.Id))
                .ToListAsync();

            decimal subtotal = services.Sum(s => s.Cost);
            decimal total = Math.Max(0, subtotal - model.Discount);

            var nextNum = await _context.Invoices.CountAsync() + 1;
            string invoiceNumber = $"INV-{DateTime.Today:yyyyMMdd}-{nextNum:D4}";

            var invoice = new Invoice
            {
                PatientId = model.PatientId,
                InvoiceNumber = invoiceNumber,
                InvoiceDate = DateTime.UtcNow,
                Subtotal = subtotal,
                Discount = model.Discount,
                TotalAmount = total,
                Status = "Pending",
                CreatedAt = DateTime.UtcNow
            };

            _context.Invoices.Add(invoice);
            await _context.SaveChangesAsync();

            foreach (var s in services)
            {
                var item = new InvoiceItem
                {
                    InvoiceId = invoice.Id,
                    ServiceId = s.Id,
                    Description = s.Name,
                    Quantity = 1,
                    UnitPrice = s.Cost,
                    Amount = s.Cost
                };
                _context.InvoiceItems.Add(item);

                if (!string.IsNullOrWhiteSpace(s.Category))
                {
                    var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                    await _deductionService.DeductSupplyForCategoryAsync(s.Category, s.Name, userId);
                }
            }

            await _context.SaveChangesAsync();

            var patient = await _context.Patients.FindAsync(model.PatientId);
            var pName = patient != null ? $"{patient.FirstName} {patient.LastName}" : $"Patient #{model.PatientId}";

            await _audit.LogAsync("Create Invoice", "Billing", $"Created invoice {invoiceNumber} for {pName} (Total: ₱{total:N2})");

            TempData["InvoiceSuccess"] = "Invoice created successfully!";
            return RedirectToAction(nameof(BillsAndPayments));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RecordPayment([Bind(Prefix = "NewPayment")] RecordPaymentViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["PaymentError"] = "Failed to record payment. Check your inputs.";
                return RedirectToAction(nameof(BillsAndPayments));
            }

            var invoice = await _context.Invoices
                .Include(i => i.Payments)
                .Include(i => i.Patient)
                .FirstOrDefaultAsync(i => i.Id == model.InvoiceId);

            if (invoice == null)
            {
                return NotFound();
            }

            var totalPaidBefore = invoice.Payments.Sum(p => p.Amount);
            var remainingBalance = invoice.TotalAmount - totalPaidBefore;

            if (model.Amount > remainingBalance)
            {
                TempData["PaymentError"] = $"Cannot record payment. Maximum amount allowed is ₱{remainingBalance:N2}.";
                return RedirectToAction(nameof(BillsAndPayments));
            }

            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            var payment = new Payment
            {
                InvoiceId = model.InvoiceId,
                Amount = model.Amount,
                ReceivedById = userId,
                PaymentDate = DateTime.UtcNow,
                PaymentMethod = model.PaymentMethod,
                ReferenceNumber = model.ReferenceNumber,
                Notes = model.Notes
            };

            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();

            var totalPaidAfter = totalPaidBefore + model.Amount;
            if (totalPaidAfter >= invoice.TotalAmount)
            {
                invoice.Status = "Paid";
            }
            else if (totalPaidAfter > 0)
            {
                invoice.Status = "PartiallyPaid";
            }

            await _context.SaveChangesAsync();

            var pName = invoice.Patient != null ? $"{invoice.Patient.FirstName} {invoice.Patient.LastName}" : "Patient";
            await _audit.LogAsync("Record Payment", "Billing", $"Recorded payment of ₱{model.Amount:N2} via {model.PaymentMethod} for {pName} (Invoice #{invoice.InvoiceNumber})");

            TempData["PaymentSuccess"] = "Payment recorded successfully!";
            return RedirectToAction(nameof(BillsAndPayments));
        }

        // =================== Promotional Messages ===================
        public async Task<IActionResult> PromotionalMessages(string search = "", string statusFilter = "", int page = 1)
        {
            const int pageSize = 6;
            var query = _context.PromotionalMessages.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(m =>
                    EF.Functions.Like(m.Name, $"%{term}%") ||
                    EF.Functions.Like(m.Email, $"%{term}%") ||
                    EF.Functions.Like(m.Phone, $"%{term}%") ||
                    EF.Functions.Like(m.Message, $"%{term}%"));
            }

            if (!string.IsNullOrWhiteSpace(statusFilter))
            {
                query = query.Where(m => m.Status == statusFilter);
            }

            var totalMessages = await query.CountAsync();
            var newCount = await _context.PromotionalMessages.CountAsync(m => m.Status == "New");
            var currentPage = Math.Max(1, page);

            var rawMessages = await query
                .OrderByDescending(m => m.CreatedAt)
                .Skip((currentPage - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var messageRows = rawMessages.Select(m => new PromotionalMessageItemViewModel
            {
                Id = m.Id,
                Name = m.Name,
                Email = m.Email,
                Phone = m.Phone,
                PreferredDate = m.PreferredDate,
                Message = m.Message,
                Status = m.Status,
                ReceptionistNotes = m.ReceptionistNotes,
                CreatedAt = m.CreatedAt,
                UpdatedAt = m.UpdatedAt
            }).ToList();

            var model = new PromotionalMessagesViewModel
            {
                Search = search,
                StatusFilter = statusFilter,
                Page = currentPage,
                PageSize = pageSize,
                TotalMessages = totalMessages,
                NewCount = newCount,
                Messages = messageRows
            };

            return View("~/Views/Receptionists/PromotionalMessages.cshtml", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateMessageStatus(int id, string status, string? notes)
        {
            var message = await _context.PromotionalMessages.FindAsync(id);
            if (message == null)
            {
                return NotFound();
            }

            message.Status = status;
            message.ReceptionistNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
            message.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            await _audit.LogAsync("Update Message Status", "Promotional Messages", $"Updated message #{id} from {message.Name} to status '{status}'");

            TempData["MessageSuccess"] = $"Message status updated to '{status}'!";
            return RedirectToAction(nameof(PromotionalMessages));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteMessage(int id)
        {
            var message = await _context.PromotionalMessages.FindAsync(id);
            if (message == null)
            {
                return NotFound();
            }

            _context.PromotionalMessages.Remove(message);
            await _context.SaveChangesAsync();

            await _audit.LogAsync("Delete Message", "Promotional Messages", $"Deleted message #{id} from {message.Name}");

            TempData["MessageSuccess"] = "Message deleted successfully!";
            return RedirectToAction(nameof(PromotionalMessages));
        }
    }
}
