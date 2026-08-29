using System.ComponentModel.DataAnnotations;

namespace DentalSync.ViewModels
{
    public class PatientListItemViewModel
    {
        public int Id { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string? MiddleName { get; set; }
        public string? Suffix { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string ContactNumber { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public class PatientManagementViewModel
    {
        public IReadOnlyList<PatientListItemViewModel> Patients { get; set; } = Array.Empty<PatientListItemViewModel>();
        public string Search { get; set; } = string.Empty;
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 8;
        public int TotalPatients { get; set; }
        public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalPatients / (double)PageSize));
    }

    public class CreatePatientViewModel
    {
        [Required(ErrorMessage = "Firstname is required.")]
        [Display(Name = "Firstname")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Lastname is required.")]
        [Display(Name = "Lastname")]
        public string LastName { get; set; } = string.Empty;

        [Display(Name = "Middlename")]
        public string? MiddleName { get; set; }

        [Display(Name = "Suffix")]
        public string? Suffix { get; set; }

        [Required(ErrorMessage = "Contact number is required.")]
        [Display(Name = "Contact number")]
        public string ContactNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Invalid email address format.")]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required.")]
        [MinLength(12, ErrorMessage = "Password must be at least 12 characters and include uppercase, lowercase, number, and special character.")]
        [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^a-zA-Z0-9]).+$", ErrorMessage = "Password must include uppercase, lowercase, number, and special character.")]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Address is required.")]
        [Display(Name = "Address")]
        public string Address { get; set; } = string.Empty;
    }
}
