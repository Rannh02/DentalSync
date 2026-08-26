using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using DentalSync.Models;

namespace DentalSync.Data
{
    public class AppDbContext : IdentityDbContext<Users>
    {
        public AppDbContext(DbContextOptions options) : base(options)
        {
        }

        // RBAC & Existing
        public DbSet<PermissionDefinition> PermissionDefinitions { get; set; }
        public DbSet<RolePermission> RolePermissions { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }

        // Clinical & Core ERP
        public DbSet<Patient> Patients { get; set; }
        public DbSet<Dentist> Dentists { get; set; }
        public DbSet<DentalService> Services { get; set; }
        public DbSet<Appointment> Appointments { get; set; }
        public DbSet<TreatmentRecord> TreatmentRecords { get; set; }

        // Billing & Payments
        public DbSet<Invoice> Invoices { get; set; }
        public DbSet<InvoiceItem> InvoiceItems { get; set; }
        public DbSet<Payment> Payments { get; set; }

        // Inventory Management
        public DbSet<InventoryCategory> InventoryCategories { get; set; }
        public DbSet<Supply> Supplies { get; set; }
        public DbSet<StockTransaction> StockTransactions { get; set; }

        // CRM & Reminders
        public DbSet<Reminder> Reminders { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Permission Definitions
            builder.Entity<PermissionDefinition>()
                .HasIndex(permission => permission.Key)
                .IsUnique();

            // Role Permissions
            builder.Entity<RolePermission>()
                .HasIndex(permission => new { permission.RoleName, permission.PermissionDefinitionId })
                .IsUnique();

            builder.Entity<RolePermission>()
                .HasOne(permission => permission.PermissionDefinition)
                .WithMany()
                .HasForeignKey(permission => permission.PermissionDefinitionId)
                .OnDelete(DeleteBehavior.Cascade);

            // Dentists & Patients
            builder.Entity<Dentist>()
                .HasOne(d => d.User)
                .WithMany()
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<Patient>()
                .HasOne(p => p.User)
                .WithMany()
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.SetNull);

            // Appointments (Restrict deletes to prevent multiple cascade paths in SQL Server)
            builder.Entity<Appointment>()
                .HasOne(a => a.Patient)
                .WithMany(p => p.Appointments)
                .HasForeignKey(a => a.PatientId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Appointment>()
                .HasOne(a => a.Dentist)
                .WithMany(d => d.Appointments)
                .HasForeignKey(a => a.DentistId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Appointment>()
                .HasOne(a => a.Service)
                .WithMany(s => s.Appointments)
                .HasForeignKey(a => a.ServiceId)
                .OnDelete(DeleteBehavior.Restrict);

            // Treatment Records
            builder.Entity<TreatmentRecord>()
                .HasOne(t => t.Appointment)
                .WithOne(a => a.TreatmentRecord)
                .HasForeignKey<TreatmentRecord>(t => t.AppointmentId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<TreatmentRecord>()
                .HasOne(t => t.Patient)
                .WithMany(p => p.TreatmentRecords)
                .HasForeignKey(t => t.PatientId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<TreatmentRecord>()
                .HasOne(t => t.Dentist)
                .WithMany(d => d.TreatmentRecords)
                .HasForeignKey(t => t.DentistId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<TreatmentRecord>()
                .HasOne(t => t.Service)
                .WithMany(s => s.TreatmentRecords)
                .HasForeignKey(t => t.ServiceId)
                .OnDelete(DeleteBehavior.Restrict);

            // Invoices & Billing
            builder.Entity<Invoice>()
                .HasIndex(i => i.InvoiceNumber)
                .IsUnique();

            builder.Entity<Invoice>()
                .HasOne(i => i.Patient)
                .WithMany(p => p.Invoices)
                .HasForeignKey(i => i.PatientId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<InvoiceItem>()
                .HasOne(ii => ii.Invoice)
                .WithMany(i => i.InvoiceItems)
                .HasForeignKey(ii => ii.InvoiceId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<InvoiceItem>()
                .HasOne(ii => ii.Service)
                .WithMany(s => s.InvoiceItems)
                .HasForeignKey(ii => ii.ServiceId)
                .OnDelete(DeleteBehavior.SetNull);

            // Payments
            builder.Entity<Payment>()
                .HasOne(p => p.Invoice)
                .WithMany(i => i.Payments)
                .HasForeignKey(p => p.InvoiceId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Payment>()
                .HasOne(p => p.ReceivedBy)
                .WithMany()
                .HasForeignKey(p => p.ReceivedById)
                .OnDelete(DeleteBehavior.SetNull);

            // Inventory
            builder.Entity<InventoryCategory>()
                .HasIndex(c => c.CategoryName)
                .IsUnique();

            builder.Entity<Supply>()
                .HasOne(s => s.Category)
                .WithMany(c => c.Supplies)
                .HasForeignKey(s => s.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<StockTransaction>()
                .HasOne(st => st.Supply)
                .WithMany(s => s.StockTransactions)
                .HasForeignKey(st => st.SupplyId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<StockTransaction>()
                .HasOne(st => st.User)
                .WithMany()
                .HasForeignKey(st => st.UserId)
                .OnDelete(DeleteBehavior.SetNull);

            // Reminders
            builder.Entity<Reminder>()
                .HasOne(r => r.Patient)
                .WithMany(p => p.Reminders)
                .HasForeignKey(r => r.PatientId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Reminder>()
                .HasOne(r => r.Appointment)
                .WithMany(a => a.Reminders)
                .HasForeignKey(r => r.AppointmentId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}
