using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DentalSync.Controllers
{
    [Authorize(Roles = "Patient")]
    public class PatientController : Controller
    {
        public IActionResult Dashboard()
        {
            return View("~/Views/Patients/Dashboard.cshtml");
        }

        public IActionResult ManageProfile()
        {
            return View("~/Views/Patients/ManageProfile.cshtml");
        }

        public IActionResult RequestAppointment()
        {
            return View("~/Views/Patients/RequestAppointment.cshtml");
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
