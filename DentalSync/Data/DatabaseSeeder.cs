using DentalSync.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DentalSync.Data
{
    public static class DatabaseSeeder
    {
        public static async Task SeedAsync(IServiceProvider serviceProvider, ILogger logger)
        {
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = serviceProvider.GetRequiredService<UserManager<Users>>();
            var dbContext = serviceProvider.GetRequiredService<AppDbContext>();

            // Ensure roles exist
            var roles = new[] { "Administrator", "Receptionist", "Dentist", "Patient" };
            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                    logger.LogInformation("Created '{Role}' role.", role);
                }
            }

            // Ensure default admin user exists
            const string adminEmail    = "admin@dentalsync.com";
            const string adminPassword = "Admin@123456";

            var adminUser = await userManager.FindByEmailAsync(adminEmail);
            if (adminUser == null)
            {
                adminUser = new Users
                {
                    FullName       = "Administrator",
                    UserName       = adminEmail,
                    Email          = adminEmail,
                    EmailConfirmed = true,
                };

                var result = await userManager.CreateAsync(adminUser, adminPassword);
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(adminUser, "Administrator");
                    logger.LogInformation("Default Administrator account created: {Email}", adminEmail);
                }
                else
                {
                    logger.LogError("Failed to create admin user: {Errors}",
                        string.Join(", ", result.Errors.Select(e => e.Description)));
                }
            }
            else if (!await userManager.IsInRoleAsync(adminUser, "Administrator"))
            {
                await userManager.AddToRoleAsync(adminUser, "Administrator");
                logger.LogInformation("Assigned Administrator role to existing user {Email}.", adminEmail);
            }

            // One-time cleanup: remove accidentally seeded dummy dentist account
            const string dummyDentistEmail = "dentist@dentalsync.com";
            var dummyDentist = await userManager.FindByEmailAsync(dummyDentistEmail);
            if (dummyDentist != null)
            {
                // Deactivate + unlink the Dentist record instead of deleting it
                // (deleting would violate FK from Appointments table)
                var dummyRecord = await dbContext.Dentists.FirstOrDefaultAsync(d => d.UserId == dummyDentist.Id);
                if (dummyRecord != null)
                {
                    dummyRecord.Status    = "Inactive";
                    dummyRecord.UserId    = null;
                    dummyRecord.UpdatedAt = DateTime.UtcNow;
                    await dbContext.SaveChangesAsync();
                }
                // Remove the Identity user account
                await userManager.DeleteAsync(dummyDentist);
                logger.LogInformation("Cleaned up dummy dentist account: {Email}", dummyDentistEmail);
            }

            // Ensure all existing dentist users have matching Dentist records
            var dentistUsers = await userManager.GetUsersInRoleAsync("Dentist");
            foreach (var user in dentistUsers)
            {
                var hasRecord = await dbContext.Dentists.AnyAsync(d => d.UserId == user.Id);
                if (!hasRecord)
                {
                    var nameParts = (user.FullName ?? "Dentist User").Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    var fName = nameParts.Length > 0 ? nameParts[0] : "Dentist";
                    var lName = nameParts.Length > 1 ? string.Join(" ", nameParts.Skip(1)) : "User";

                    var dentist = new Dentist
                    {
                        UserId = user.Id,
                        FirstName = fName,
                        LastName = lName,
                        Email = user.Email,
                        Specialization = "General Dentistry",
                        Status = "Active",
                        CreatedAt = DateTime.UtcNow
                    };
                    dbContext.Dentists.Add(dentist);
                    logger.LogInformation("Automatically created Dentist record for existing user {Email}", user.Email);
                }
            }
            await dbContext.SaveChangesAsync();
        }
    }
}
