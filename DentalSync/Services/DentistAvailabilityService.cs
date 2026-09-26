using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DentalSync.Data;
using DentalSync.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace DentalSync.Services
{
    public class DentistAvailabilityService : IDentistAvailabilityService
    {
        private readonly AppDbContext _context;

        public DentistAvailabilityService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<bool> IsDentistAvailableAsync(int dentistId, DateOnly appointmentDate, TimeOnly startTime, TimeOnly? endTime = null, int? excludeAppointmentId = null)
        {
            var dentist = await _context.Dentists
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == dentistId && d.Status == "Active");

            if (dentist == null)
                return false;

            // 1. Check Working Days
            var dayOfWeekName = appointmentDate.DayOfWeek.ToString();
            var workingDaysStr = string.IsNullOrWhiteSpace(dentist.WorkingDays)
                ? "Monday,Tuesday,Wednesday,Thursday,Friday"
                : dentist.WorkingDays;

            var workingDaysList = workingDaysStr.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (!workingDaysList.Contains(dayOfWeekName, StringComparer.OrdinalIgnoreCase))
            {
                return false; // Dentist does not work on this day of the week
            }

            // 2. Check Working Hours
            var targetEnd = endTime ?? startTime.AddMinutes(45);
            var workStart = dentist.WorkingStartTime == default ? new TimeOnly(9, 0) : dentist.WorkingStartTime;
            var workEnd = dentist.WorkingEndTime == default ? new TimeOnly(17, 0) : dentist.WorkingEndTime;

            if (startTime < workStart || targetEnd > workEnd)
            {
                return false; // Appointment time is outside dentist's working hours
            }

            // 3. Check Appointment Conflicts
            var existingAppointments = await _context.Appointments
                .AsNoTracking()
                .Where(a => a.DentistId == dentistId &&
                            a.AppointmentDate == appointmentDate &&
                            a.Status != "Cancelled" &&
                            a.Status != "NoShow")
                .Where(a => !excludeAppointmentId.HasValue || a.Id != excludeAppointmentId.Value)
                .ToListAsync();

            foreach (var appt in existingAppointments)
            {
                var apptEnd = appt.EndTime ?? appt.StartTime.AddMinutes(45);
                if (appt.StartTime < targetEnd && startTime < apptEnd)
                {
                    return false; // Time conflict found
                }
            }

            return true;
        }

        public async Task<List<DentistAvailabilityOptionViewModel>> GetDentistAvailabilityOptionsAsync(
            DateOnly appointmentDate,
            TimeOnly startTime,
            TimeOnly? endTime = null,
            int? currentDentistId = null,
            int? excludeAppointmentId = null)
        {
            var activeDentists = await _context.Dentists
                .AsNoTracking()
                .Where(d => d.Status == "Active")
                .Where(d => !currentDentistId.HasValue || d.Id != currentDentistId.Value)
                .OrderBy(d => d.LastName)
                .ThenBy(d => d.FirstName)
                .ToListAsync();

            if (!activeDentists.Any())
                return new List<DentistAvailabilityOptionViewModel>();

            var dentistIds = activeDentists.Select(d => d.Id).ToList();
            var targetEnd = endTime ?? startTime.AddMinutes(45);
            var dayOfWeekName = appointmentDate.DayOfWeek.ToString();

            var appointmentsOnDate = await _context.Appointments
                .AsNoTracking()
                .Where(a => dentistIds.Contains(a.DentistId) &&
                            a.AppointmentDate == appointmentDate &&
                            a.Status != "Cancelled" &&
                            a.Status != "NoShow")
                .Where(a => !excludeAppointmentId.HasValue || a.Id != excludeAppointmentId.Value)
                .ToListAsync();

            var conflictingDentistIds = new HashSet<int>();
            foreach (var appt in appointmentsOnDate)
            {
                var apptEnd = appt.EndTime ?? appt.StartTime.AddMinutes(45);
                if (appt.StartTime < targetEnd && startTime < apptEnd)
                {
                    conflictingDentistIds.Add(appt.DentistId);
                }
            }

            var options = new List<DentistAvailabilityOptionViewModel>();
            foreach (var d in activeDentists)
            {
                var isAvailable = true;

                // Check Working Days
                var workingDaysStr = string.IsNullOrWhiteSpace(d.WorkingDays)
                    ? "Monday,Tuesday,Wednesday,Thursday,Friday"
                    : d.WorkingDays;
                var workingDaysList = workingDaysStr.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (!workingDaysList.Contains(dayOfWeekName, StringComparer.OrdinalIgnoreCase))
                {
                    isAvailable = false;
                }

                // Check Working Hours
                var workStart = d.WorkingStartTime == default ? new TimeOnly(9, 0) : d.WorkingStartTime;
                var workEnd = d.WorkingEndTime == default ? new TimeOnly(17, 0) : d.WorkingEndTime;
                if (startTime < workStart || targetEnd > workEnd)
                {
                    isAvailable = false;
                }

                // Check Appointment Conflict
                if (conflictingDentistIds.Contains(d.Id))
                {
                    isAvailable = false;
                }

                options.Add(new DentistAvailabilityOptionViewModel
                {
                    DentistId = d.Id,
                    DentistName = $"Dr. {d.FirstName} {d.LastName}",
                    IsAvailable = isAvailable
                });
            }

            return options;
        }
    }
}
