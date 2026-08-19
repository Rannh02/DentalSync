using System.ComponentModel.DataAnnotations;

namespace DentalSync.ViewModels
{
    public static class UserRoles
    {
        public static readonly string[] All =
        {
            "Administrator",
            "Receptionist",
            "Dentist",
            "Patient"
        };

        public static readonly string[] Creatable =
        {
            "Receptionist",
            "Dentist",
            "Patient"
        };
    }

    public class UserListItemViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = "Unassigned";
        public bool IsActive { get; set; }
        public DateTime? LockoutEnd { get; set; }
    }

    public class UserManagementViewModel
    {
        public IReadOnlyList<UserListItemViewModel> Users { get; set; } = Array.Empty<UserListItemViewModel>();
        public string Search { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 8;
        public int TotalUsers { get; set; }
        public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalUsers / (double)PageSize));
        public string[] Roles { get; } = UserRoles.All;
    }

    public class CreateUserViewModel
    {
        [Required]
        [Display(Name = "Full name")]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Username")]
        public string UserName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MinLength(12, ErrorMessage = "Password must be at least 12 characters and include uppercase, lowercase, number, and special character.")]
        [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^a-zA-Z0-9]).+$", ErrorMessage = "Password must include uppercase, lowercase, number, and special character.")]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; } = string.Empty;

        [Required]
        public string Role { get; set; } = "Patient";

        public string[] Roles { get; } = UserRoles.Creatable;
    }

    public class EditUserViewModel
    {
        public string Id { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Full name")]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Role { get; set; } = "Patient";

        public string[] Roles { get; } = UserRoles.All;
    }

    public class ResetUserPasswordViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;

        [Required]
        [StringLength(40, MinimumLength = 8)]
        [DataType(DataType.Password)]
        [Display(Name = "New password")]
        public string NewPassword { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        [Compare(nameof(NewPassword))]
        [Display(Name = "Confirm password")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
