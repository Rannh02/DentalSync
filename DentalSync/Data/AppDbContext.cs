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

        public DbSet<PermissionDefinition> PermissionDefinitions { get; set; }
        public DbSet<RolePermission> RolePermissions { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<PermissionDefinition>()
                .HasIndex(permission => permission.Key)
                .IsUnique();

            builder.Entity<RolePermission>()
                .HasIndex(permission => new { permission.RoleName, permission.PermissionDefinitionId })
                .IsUnique();

            builder.Entity<RolePermission>()
                .HasOne(permission => permission.PermissionDefinition)
                .WithMany()
                .HasForeignKey(permission => permission.PermissionDefinitionId)
                .OnDelete(DeleteBehavior.Cascade);
        }

    }
}
