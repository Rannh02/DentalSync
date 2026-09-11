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

        public ReceptionistController(
            AppDbContext context,
            UserManager<Users> userManager,
            RoleManager<IdentityRole> roleManager,
            AuditService audit,
            InventoryDeductionService deductionService)
        {
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
            _audit = audit;
            _deductionService = deductionService;
        }

        public IActionResult Receptionist_Dashboard()
        {
            return View("~/Views/Receptionists/Receptionist_Dashboard.cshtml");
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
            // Get all appointments for this patient (any status) and collect their services
            var appointments = await _context.Appointments
                .Where(a => a.PatientId == patientId)
                .Include(a => a.Service)
                .OrderByDescending(a => a.AppointmentDate)
                .ThenByDescending(a => a.StartTime)
                .ToListAsync();

            if (appointments.Count == 0)
                return Json(new { services = Array.Empty<object>() });

            // Return distinct services across all appointments
            var services = appointments
                .Where(a => a.Service != null)
                .GroupBy(a => a.Service.Id)
                .Select(g => new
                {
                    id   = g.Key,
                    name = g.First().Service.Name,
                    cost = g.First().Service.Cost
                })
                .ToArray();

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
