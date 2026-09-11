using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using DentalSync.Models;

namespace DentalSync.ViewModels
{
    public class AppointmentListItemViewModel
    {
        public int Id { get; set; }
        public int PatientId { get; set; }
        public string PatientName { get; set; } = string.Empty;
        public int DentistId { get; set; }
        public string DentistName { get; set; } = string.Empty;
        public int ServiceId { get; set; }
        public string ServiceName { get; set; } = string.Empty;
        /// <summary>Comma-joined display names of all services for this appointment.</summary>
        public string ServiceNames { get; set; } = string.Empty;
        public decimal TotalCost { get; set; }
        public DateOnly AppointmentDate { get; set; }
        public TimeOnly StartTime { get; set; }
        public TimeOnly? EndTime { get; set; }
        public string Status { get; set; } = "Scheduled";
        public string? Notes { get; set; }
    }

    public class ManageAppointmentsViewModel
    {
        public List<AppointmentListItemViewModel> Appointments { get; set; } = new();
        public List<Patient> Patients { get; set; } = new();
        public List<Dentist> Dentists { get; set; } = new();
        public List<DentalService> Services { get; set; } = new();
        public CreateAppointmentViewModel NewAppointment { get; set; } = new();
    }

    public class CreateAppointmentViewModel
    {
        [Required(ErrorMessage = "Patient is required")]
        public int PatientId { get; set; }

        [Required(ErrorMessage = "Dentist is required")]
        public int DentistId { get; set; }

        /// <summary>All selected service IDs (multi-select). First item is stored as primary ServiceId.</summary>
        public List<int> SelectedServiceIds { get; set; } = new();

        [Required(ErrorMessage = "Date is required")]
        [DataType(DataType.Date)]
        public DateOnly AppointmentDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

        [Required(ErrorMessage = "Start Time is required")]
        [DataType(DataType.Time)]
        public TimeOnly StartTime { get; set; } = new TimeOnly(9, 0);

        public string? Notes { get; set; }
    }

    public class InvoiceListItemViewModel
    {
        public int Id { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public string PatientName { get; set; } = string.Empty;
        public DateTime InvoiceDate { get; set; }
        public decimal Subtotal { get; set; }
        public decimal Discount { get; set; }
        public decimal TotalAmount { get; set; }
        public string Status { get; set; } = "Pending";
        public decimal AmountPaid { get; set; }
        public decimal Balance => TotalAmount - AmountPaid;
        public List<string> Items { get; set; } = new();
    }

    public class BillsAndPaymentsViewModel
    {
        public List<InvoiceListItemViewModel> Invoices { get; set; } = new();
        public List<Patient> Patients { get; set; } = new();
        public List<DentalService> Services { get; set; } = new();
        public CreateInvoiceViewModel NewInvoice { get; set; } = new();
        public RecordPaymentViewModel NewPayment { get; set; } = new();
    }

    public class CreateInvoiceViewModel
    {
        [Required(ErrorMessage = "Patient is required")]
        public int PatientId { get; set; }

        [Range(0, 100000, ErrorMessage = "Discount must be positive")]
        public decimal Discount { get; set; } = 0;

        [Required(ErrorMessage = "Select at least one service")]
        public List<int> SelectedServiceIds { get; set; } = new();
    }

    public class RecordPaymentViewModel
    {
        [Required(ErrorMessage = "Invoice ID is required")]
        public int InvoiceId { get; set; }

        [Required(ErrorMessage = "Amount is required")]
        [Range(0.01, 1000000, ErrorMessage = "Amount must be greater than 0")]
        public decimal Amount { get; set; }

        [Required(ErrorMessage = "Payment Method is required")]
        public string PaymentMethod { get; set; } = "Cash";

        public string? ReferenceNumber { get; set; }
        public string? Notes { get; set; }
    }

    public class RequestAppointmentViewModel
    {
        public List<AppointmentListItemViewModel> MyAppointments { get; set; } = new();
    }

    public class DentistViewAppointmentsViewModel
    {
        public List<AppointmentListItemViewModel> Appointments { get; set; } = new();
    }

    public class PromotionalMessageItemViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public DateTime? PreferredDate { get; set; }
        public string Message { get; set; } = string.Empty;
        public string Status { get; set; } = "New";
        public string? ReceptionistNotes { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class PromotionalMessagesViewModel
    {
        public string Search { get; set; } = string.Empty;
        public string StatusFilter { get; set; } = string.Empty;
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 6;
        public int TotalMessages { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalMessages / PageSize);
        public int NewCount { get; set; }
        public List<PromotionalMessageItemViewModel> Messages { get; set; } = new();
    }
}
