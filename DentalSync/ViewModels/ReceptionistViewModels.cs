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

        [Required(ErrorMessage = "Service is required")]
        public int ServiceId { get; set; }

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
}
