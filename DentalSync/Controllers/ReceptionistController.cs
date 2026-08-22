using DentalSync.Data;
using DentalSync.Models;
using DentalSync.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace DentalSync.Controllers
{
    [Authorize(Roles = "Receptionist")]
    public class ReceptionistController : Controller
    {
        private readonly AppDbContext _context;

        public ReceptionistController(AppDbContext context)
        {
            _context = context;
        }

        public IActionResult Receptionist_Dashboard()
        {
            return View("~/Views/Receptionists/Receptionist_Dashboard.cshtml");
        }

        public async Task<IActionResult> Register_Patients(string search = "", int page = 1)
        {
            const int pageSize = 8;
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

            var patient = new Patient
            {
                FirstName = model.FirstName.Trim(),
                LastName = model.LastName.Trim(),
                MiddleName = string.IsNullOrWhiteSpace(model.MiddleName) ? null : model.MiddleName.Trim(),
                Suffix = string.IsNullOrWhiteSpace(model.Suffix) ? null : model.Suffix.Trim(),
                ContactNumber = model.ContactNumber.Trim(),
                Address = model.Address.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            _context.Patients.Add(patient);
            await _context.SaveChangesAsync();

            TempData["PatientSuccess"] = "Patient registered successfully!";
            return RedirectToAction(nameof(Register_Patients));
        }

        public IActionResult View_Patients()
        {
            return View("~/Views/Receptionists/View_Patients.cshtml");
        }

        public IActionResult Bills_Patients()
        {
            return View("~/Views/Receptionists/Bills_Patients.cshtml");
        }
    }
}
