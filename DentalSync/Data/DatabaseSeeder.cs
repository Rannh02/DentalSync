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

            // Ensure TermsAndConditions table exists and has default content
            try
            {
                await dbContext.Database.ExecuteSqlRawAsync(@"
                    CREATE TABLE IF NOT EXISTS `TermsAndConditions` (
                        `Id` INT AUTO_INCREMENT NOT NULL,
                        `Title` VARCHAR(200) NOT NULL,
                        `Content` LONGTEXT NOT NULL,
                        `UpdatedAt` DATETIME NOT NULL,
                        `UpdatedBy` VARCHAR(150) NULL,
                        PRIMARY KEY (`Id`)
                    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
                ");

                if (!await dbContext.TermsAndConditions.AnyAsync())
                {
                    dbContext.TermsAndConditions.Add(new TermsAndConditions
                    {
                        Title = "DentalSync Subscription Terms & Conditions",
                        Content = @"WELCOME TO DENTALSYNC CLINIC MANAGEMENT SYSTEM

Please read these Terms and Conditions carefully before completing your subscription registration.

1. ACCEPTANCE OF TERMS
By subscribing to DentalSync Clinic Management System (""Service""), you agree to be bound by these Terms and Conditions. If you do not agree to all terms, you may not complete subscription or use our software platform.

2. SUBSCRIPTION & LICENSE
DentalSync grants your clinic a non-exclusive, non-transferable subscription license to access and use our web-based dental practice management platform during the active subscription period (Monthly or Annual).

3. CLINIC DATA & PRIVACY
- All patient records, clinical notes, treatment plans, and clinic data remain the exclusive property of your dental clinic.
- DentalSync implements industry-standard 256-bit encryption and security measures to protect your clinic and patient records against unauthorized access.

4. BILLING, PAYMENTS & RENEWAL
- Subscription fees are billed in advance based on your selected billing cycle (Monthly or Annual).
- Automated payments are processed securely via PayMongo (GCash, Credit/Debit Card, Maya).
- Subscriptions auto-renew unless cancelled prior to the next billing date. Refunds are provided in accordance with applicable consumer rights.

5. SYSTEM AVAILABILITY & SUPPORT
- We maintain a target system uptime of 99.9%. Scheduled system updates and maintenance windows will be communicated to clinic administrators in advance.
- Technical customer support is available via support@dentalsync.ph.

6. ACCOUNT RESPONSIBILITIES
- Clinic Administrators are responsible for safeguarding their login credentials and managing staff role permissions within their clinic portal.",
                        UpdatedAt = DateTime.UtcNow,
                        UpdatedBy = "System Seeder"
                    });
                    await dbContext.SaveChangesAsync();
                    logger.LogInformation("Seeded default Terms and Conditions.");
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to verify or seed TermsAndConditions table.");
            }

            try
            {
                await dbContext.Database.ExecuteSqlRawAsync(@"
                    CREATE TABLE IF NOT EXISTS `PromotionalMessages` (
                        `Id` INT NOT NULL AUTO_INCREMENT,
                        `Name` VARCHAR(100) NOT NULL,
                        `Email` VARCHAR(150) NOT NULL,
                        `Phone` VARCHAR(30) NOT NULL,
                        `PreferredDate` DATETIME(6) NULL,
                        `Message` LONGTEXT NOT NULL,
                        `Status` VARCHAR(30) NOT NULL DEFAULT 'New',
                        `ReceptionistNotes` LONGTEXT NULL,
                        `CreatedAt` DATETIME(6) NOT NULL,
                        `UpdatedAt` DATETIME(6) NULL,
                        PRIMARY KEY (`Id`)
                    );
                ");
                logger.LogInformation("Ensured PromotionalMessages table exists.");
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to create or verify PromotionalMessages table.");
            }

            // Seed Default Inventory Categories & Dental Supplies
            try
            {
                if (!await dbContext.InventoryCategories.AnyAsync())
                {
                    var ppeCat = new InventoryCategory { CategoryName = "Personal Protective Equipment", Description = "Gloves, masks, bibs, and protective wear." };
                    var consumablesCat = new InventoryCategory { CategoryName = "Dental Consumables", Description = "Saliva ejectors, cotton rolls, pouches, and needles." };
                    var anestheticsCat = new InventoryCategory { CategoryName = "Anesthetics & Medications", Description = "Local anesthetics, topical gels, and cartridges." };
                    var restorativeCat = new InventoryCategory { CategoryName = "Surgical & Restorative", Description = "Composites, bonding agents, alloys, and cements." };
                    var impressionCat = new InventoryCategory { CategoryName = "Impression Materials", Description = "Alginates, silicone, and impression trays." };

                    dbContext.InventoryCategories.AddRange(ppeCat, consumablesCat, anestheticsCat, restorativeCat, impressionCat);
                    await dbContext.SaveChangesAsync();

                    if (!await dbContext.Supplies.AnyAsync())
                    {
                        var supplies = new List<Supply>
                        {
                            new Supply
                            {
                                CategoryId = ppeCat.Id,
                                SupplyName = "Nitrile Dental Examination Gloves (M)",
                                Description = "Powder-free nitrile examination gloves, 100 pcs per box.",
                                Unit = "Box",
                                Quantity = 50,
                                MinimumStock = 10,
                                PurchasePrice = 350.00m,
                                Status = "In Stock",
                                CreatedAt = DateTime.UtcNow
                            },
                            new Supply
                            {
                                CategoryId = ppeCat.Id,
                                SupplyName = "3-Ply Disposable Surgical Face Masks",
                                Description = "Fluid resistant 3-ply earloop masks, 50 pcs per box.",
                                Unit = "Box",
                                Quantity = 40,
                                MinimumStock = 10,
                                PurchasePrice = 150.00m,
                                Status = "In Stock",
                                CreatedAt = DateTime.UtcNow
                            },
                            new Supply
                            {
                                CategoryId = consumablesCat.Id,
                                SupplyName = "Saliva Ejector Suction Tips",
                                Description = "Disposable clear flexible suction tips, 100 pcs per pack.",
                                Unit = "Pack",
                                Quantity = 45,
                                MinimumStock = 10,
                                PurchasePrice = 220.00m,
                                Status = "In Stock",
                                CreatedAt = DateTime.UtcNow
                            },
                            new Supply
                            {
                                CategoryId = consumablesCat.Id,
                                SupplyName = "Self-Sealing Sterilization Pouches (3.5\" x 9\")",
                                Description = "Autoclave sterilization pouches, 200 pcs per box.",
                                Unit = "Box",
                                Quantity = 30,
                                MinimumStock = 8,
                                PurchasePrice = 380.00m,
                                Status = "In Stock",
                                CreatedAt = DateTime.UtcNow
                            },
                            new Supply
                            {
                                CategoryId = anestheticsCat.Id,
                                SupplyName = "Lidocaine HCl 2% with Epinephrine Cartridges",
                                Description = "Local anesthetic cartridges for dental procedures, 50 cartridges per box.",
                                Unit = "Box",
                                Quantity = 25,
                                MinimumStock = 5,
                                PurchasePrice = 1250.00m,
                                Status = "In Stock",
                                CreatedAt = DateTime.UtcNow
                            },
                            new Supply
                            {
                                CategoryId = anestheticsCat.Id,
                                SupplyName = "Disposable Dental Needles 30G Short",
                                Description = "Sterile single-use dental needles, 100 pcs per box.",
                                Unit = "Box",
                                Quantity = 35,
                                MinimumStock = 5,
                                PurchasePrice = 400.00m,
                                Status = "In Stock",
                                CreatedAt = DateTime.UtcNow
                            },
                            new Supply
                            {
                                CategoryId = restorativeCat.Id,
                                SupplyName = "Universal Nano-Hybrid Composite Resin (Shade A2)",
                                Description = "Light-cured restorative composite syringe, 4g.",
                                Unit = "Syringe",
                                Quantity = 20,
                                MinimumStock = 4,
                                PurchasePrice = 850.00m,
                                Status = "In Stock",
                                CreatedAt = DateTime.UtcNow
                            },
                            new Supply
                            {
                                CategoryId = impressionCat.Id,
                                SupplyName = "Alginate Impression Powder (Dust-free)",
                                Description = "Fast-set chromatic alginate impression material, 454g bag.",
                                Unit = "Bag",
                                Quantity = 18,
                                MinimumStock = 5,
                                PurchasePrice = 450.00m,
                                Status = "In Stock",
                                CreatedAt = DateTime.UtcNow
                            }
                        };

                        dbContext.Supplies.AddRange(supplies);
                        await dbContext.SaveChangesAsync();

                        var stockTxns = supplies.Select(s => new StockTransaction
                        {
                            SupplyId = s.Id,
                            UserId = superadminUser?.Id,
                            TransactionType = "In",
                            Quantity = s.Quantity,
                            TransactionDate = DateTime.UtcNow,
                            Reference = "INIT-STOCK",
                            Notes = "Initial inventory stock intake"
                        }).ToList();

                        dbContext.StockTransactions.AddRange(stockTxns);
                        await dbContext.SaveChangesAsync();
                        logger.LogInformation("Seeded default inventory categories, supplies, and stock transactions.");
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to seed default inventory categories and supplies.");
            }
        }
    }
}
