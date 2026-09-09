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
            var roles = new[] { "Superadmin", "Administrator", "Receptionist", "Dentist", "Patient" };
            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                    logger.LogInformation("Created '{Role}' role.", role);
                }
            }

            // Ensure default Superadmin user exists
            const string superadminEmail    = "superadmin@dentalsync.ph";
            const string superadminPassword = "Superadmin@123";

            var superadminUser = await userManager.FindByEmailAsync(superadminEmail)
                ?? await userManager.FindByNameAsync(superadminEmail);

            if (superadminUser == null)
            {
                superadminUser = new Users
                {
                    FullName       = "Super Admin",
                    UserName       = superadminEmail,
                    Email          = superadminEmail,
                    EmailConfirmed = true,
                    LockoutEnabled = false
                };

                var result = await userManager.CreateAsync(superadminUser, superadminPassword);
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(superadminUser, "Superadmin");
                    logger.LogInformation("Default Superadmin account created: {Email}", superadminEmail);
                }
                else
                {
                    logger.LogError("Failed to create superadmin user: {Errors}",
                        string.Join(", ", result.Errors.Select(e => e.Description)));
                }
            }
            else
            {
                superadminUser.EmailConfirmed = true;
                superadminUser.LockoutEnd = null;
                superadminUser.LockoutEnabled = false;
                await userManager.UpdateAsync(superadminUser);

                if (!await userManager.IsInRoleAsync(superadminUser, "Superadmin"))
                {
                    await userManager.AddToRoleAsync(superadminUser, "Superadmin");
                    logger.LogInformation("Assigned Superadmin role to existing user {Email}.", superadminEmail);
                }

                // Reset password to guarantee credentials work
                var token = await userManager.GeneratePasswordResetTokenAsync(superadminUser);
                var resetResult = await userManager.ResetPasswordAsync(superadminUser, token, superadminPassword);
                if (resetResult.Succeeded)
                {
                    logger.LogInformation("Superadmin password successfully updated.");
                }
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
