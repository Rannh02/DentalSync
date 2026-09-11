using System.ComponentModel.DataAnnotations;

namespace DentalSync.ViewModels
{
    public class ServiceListItemViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal Cost { get; set; }
        public int EstimatedDurationMinutes { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class CreateServiceViewModel
    {
        [Required(ErrorMessage = "Service name is required.")]
        [StringLength(100, ErrorMessage = "Service name cannot exceed 100 characters.")]
        public string Name { get; set; } = string.Empty;

        [StringLength(100, ErrorMessage = "Category cannot exceed 100 characters.")]
        public string Category { get; set; } = string.Empty;

        [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters.")]
        public string? Description { get; set; }

        [Required(ErrorMessage = "Cost is required.")]
        [Range(0, 1000000, ErrorMessage = "Cost must be a positive amount.")]
        public decimal Cost { get; set; }

        [Range(1, 1440, ErrorMessage = "Duration must be between 1 and 1440 minutes.")]
        public int EstimatedDurationMinutes { get; set; } = 30;
    }

    public class EditServiceViewModel : CreateServiceViewModel
    {
        [Required]
        public int Id { get; set; }

        public bool IsActive { get; set; }
    }

    public class ServiceManagementViewModel
    {
        public string Search { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 8;
        public int TotalServices { get; set; }
        public List<ServiceListItemViewModel> Services { get; set; } = new();

        public int TotalPages => (int)Math.Ceiling((double)TotalServices / PageSize);
        public bool HasPreviousPage => Page > 1;
        public bool HasNextPage => Page < TotalPages;
    }
}
