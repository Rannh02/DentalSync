using System;
using System.Collections.Generic;

namespace DentalSync.ViewModels
{
    public class DashboardAppointmentItemViewModel
    {
        public int Id { get; set; }
        public string PatientName { get; set; } = string.Empty;
        public string Initials { get; set; } = string.Empty;
        public string ServiceName { get; set; } = string.Empty;
        public string DentistName { get; set; } = string.Empty;
        public DateOnly AppointmentDate { get; set; }
        public TimeOnly StartTime { get; set; }
        public TimeOnly? EndTime { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Status { get; set; } = "Scheduled";

        public string StatusCssClass => Status?.ToLower() switch
        {
            "confirmed" => "dc-status-confirmed",
            "completed" => "dc-status-confirmed",
            "pending" => "dc-status-pending",
            "scheduled" => "dc-status-pending",
            "cancelled" => "dc-status-cancelled",
            _ => "dc-status-pending"
        };
    }

    public class DashboardActivityItemViewModel
    {
        public int Id { get; set; }
        public string User { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string Module { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string RelativeTimeText { get; set; } = string.Empty;

        public string Icon
        {
            get
            {
                var act = Action?.ToLower() ?? "";
                var mod = Module?.ToLower() ?? "";
                if (act.Contains("payment") || act.Contains("invoice") || mod.Contains("billing")) return "₱";
                if (act.Contains("patient") || act.Contains("register") || mod.Contains("patient")) return "+";
                if (act.Contains("appointment") || act.Contains("booking") || mod.Contains("appointment")) return "✓";
                return "i";
            }
        }

        public string IconCssClass
        {
            get
            {
                var act = Action?.ToLower() ?? "";
                var mod = Module?.ToLower() ?? "";
                if (act.Contains("payment") || act.Contains("invoice") || mod.Contains("billing")) return "dc-icon-green";
                if (act.Contains("patient") || act.Contains("register") || mod.Contains("patient")) return "dc-icon-blue";
                if (act.Contains("appointment") || act.Contains("booking") || mod.Contains("appointment")) return "dc-icon-teal";
                return "dc-icon-amber";
            }
        }
    }

    public class AdminDashboardViewModel
    {
        public int TotalPatients { get; set; }
        public int TodayAppointmentsCount { get; set; }
        public int TotalAppointmentsCount { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal PendingPayments { get; set; }
        public List<DashboardAppointmentItemViewModel> TodayAppointments { get; set; } = new();
        public List<DashboardActivityItemViewModel> RecentActivities { get; set; } = new();
    }
}
