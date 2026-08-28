using System;
using System.Collections.Generic;

namespace DentalSync.ViewModels
{
    // ── KPI summary ────────────────────────────────────────────────────────────
    public class ReportsKpiViewModel
    {
        public decimal TotalRevenue { get; set; }
        public int TotalAppointments { get; set; }
        public int NewPatients { get; set; }
        public int CompletedTreatments { get; set; }
        public decimal OutstandingPayments { get; set; }
        public int InventoryAlerts { get; set; }

        // period deltas (% vs previous period) – nullable = not computed
        public double? RevenueDelta { get; set; }
        public double? AppointmentsDelta { get; set; }
        public double? PatientsDelta { get; set; }
        public double? TreatmentsDelta { get; set; }
    }

    // ── Chart data point ────────────────────────────────────────────────────────
    public class ChartPoint
    {
        public string Label { get; set; } = string.Empty;
        public decimal Value { get; set; }
        public int Count { get; set; }
    }

    // ── Financial stats ────────────────────────────────────────────────────────
    public class FinancialStatsViewModel
    {
        public decimal TotalRevenue { get; set; }
        public decimal PaidInvoices { get; set; }
        public decimal UnpaidInvoices { get; set; }
        public decimal PartiallyPaid { get; set; }
        public List<ChartPoint> PaymentMethods { get; set; } = new();
    }

    // ── Appointment status ──────────────────────────────────────────────────────
    public class AppointmentStatsViewModel
    {
        public int Scheduled { get; set; }
        public int Confirmed { get; set; }
        public int Completed { get; set; }
        public int Cancelled { get; set; }
        public int NoShow { get; set; }
        public int Total => Scheduled + Confirmed + Completed + Cancelled + NoShow;
    }

    // ── Inventory stats ────────────────────────────────────────────────────────
    public class InventoryStatsViewModel
    {
        public int TotalSupplies { get; set; }
        public int LowStock { get; set; }
        public int OutOfStock { get; set; }
        public int ExpiringIn30Days { get; set; }
    }

    // ── Top services row ───────────────────────────────────────────────────────
    public class TopServiceRow
    {
        public string ServiceName { get; set; } = string.Empty;
        public int Count { get; set; }
        public decimal Revenue { get; set; }
    }

    // ── Recent invoices table ──────────────────────────────────────────────────
    public class RecentInvoiceRow
    {
        public int InvoiceId { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public string PatientName { get; set; } = string.Empty;
        public DateTime InvoiceDate { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal PaidAmount { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    // ── Main page ViewModel ────────────────────────────────────────────────────
    public class ReportsViewModel
    {
        // Date range filter
        public string Preset { get; set; } = "month";   // today | week | month | custom
        public DateTime DateFrom { get; set; }
        public DateTime DateTo { get; set; }

        // Sections
        public ReportsKpiViewModel Kpi { get; set; } = new();
        public FinancialStatsViewModel Financial { get; set; } = new();
        public AppointmentStatsViewModel AppointmentStats { get; set; } = new();
        public InventoryStatsViewModel InventoryStats { get; set; } = new();

        // Chart series
        public List<ChartPoint> RevenueByMonth { get; set; } = new();
        public List<ChartPoint> PatientGrowth { get; set; } = new();
        public List<TopServiceRow> TopServices { get; set; } = new();

        // Security KPIs & Charts
        public int ActiveSessions { get; set; }
        public int FailedLogins { get; set; }
        public int LockedAccounts { get; set; }
        public List<ChartPoint> LoginsByMonth { get; set; } = new();
        public List<ChartPoint> FailedLoginsByMonth { get; set; } = new();
        public List<ChartPoint> BrowserStats { get; set; } = new();

        // Recent invoices table
        public List<RecentInvoiceRow> RecentInvoices { get; set; } = new();
        public int InvoicePage { get; set; } = 1;
        public int InvoicePageSize { get; set; } = 8;
        public int TotalInvoices { get; set; }
        public int TotalInvoicePages => Math.Max(1, (int)Math.Ceiling(TotalInvoices / (double)InvoicePageSize));
    }
}
