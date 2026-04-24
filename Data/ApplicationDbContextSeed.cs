using Microsoft.EntityFrameworkCore;
using MyMvcApp.Models;

namespace MyMvcApp.Data
{
    public static class ApplicationDbContextSeed
    {
        public static async Task SeedAsync(ApplicationDbContext context)
        {
            // Ensure database is created
            await context.Database.EnsureCreatedAsync();

            // Check if admin account already exists
            var existingAdmin = await context.Accounts
                .FirstOrDefaultAsync(a => a.Role == UserRole.Admin);

            if (existingAdmin == null)
            {
                // Create default admin account
                var adminAccount = new Account
                {
                    Username = "admin",
                    Email = "admin@ssg.com",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
                    Role = UserRole.Admin,
                    RequestStatus = RequestStatus.Approve, // Auto-approve admin account
                    IsOnline = false,
                    CreatedAt = DateTime.UtcNow
                };

                context.Accounts.Add(adminAccount);
                await context.SaveChangesAsync();
                
                System.Console.WriteLine("Default admin account created:");
                System.Console.WriteLine("Email: admin@ssg.com");
                System.Console.WriteLine("Password: admin123");
            }
        }
    }
}
