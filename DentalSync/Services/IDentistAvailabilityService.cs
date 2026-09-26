using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DentalSync.ViewModels;

namespace DentalSync.Services
{
    public interface IDentistAvailabilityService
    {
        Task<bool> IsDentistAvailableAsync(int dentistId, DateOnly appointmentDate, TimeOnly startTime, TimeOnly? endTime = null, int? excludeAppointmentId = null);
        Task<List<DentistAvailabilityOptionViewModel>> GetDentistAvailabilityOptionsAsync(DateOnly appointmentDate, TimeOnly startTime, TimeOnly? endTime = null, int? currentDentistId = null, int? excludeAppointmentId = null);
    }
}
