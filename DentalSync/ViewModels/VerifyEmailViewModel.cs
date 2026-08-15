using System.ComponentModel.DataAnnotations;

namespace DentalSync.ViewModels
{
    public class VerifyEmailViewModel
    {
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress]
        public string Email { get; set; } 
    }
}
