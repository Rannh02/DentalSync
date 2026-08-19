using DentalSync.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System.Diagnostics;

namespace DentalSync.Controllers
{
    public class ReceptionistController : Controller
    {

        public IActionResult Register_Patients()
        {
            return View();
        }
    }
}
