using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DentalSync.Controllers
{
    [Authorize(Roles = "Dentist")]
    public class DentistController : Controller
    {
        public IActionResult Dashboard()
        {
            return View("~/Views/Dentist/Dashboard.cshtml");
        }

        public IActionResult ViewPatientRecords()
        {
            return View("~/Views/Dentist/ViewPatientRecords.cshtml");
        }

        public IActionResult ViewAppointments()
        {
            return View("~/Views/Dentist/ViewAppointments.cshtml");
        }

        public IActionResult UpdateDentalHistory()
        {
            return View("~/Views/Dentist/UpdateDentalHistory.cshtml");
        }

        public IActionResult TreatmentRecords()
        {
            return View("~/Views/Dentist/TreatmentRecords.cshtml");
        }
    }
}
