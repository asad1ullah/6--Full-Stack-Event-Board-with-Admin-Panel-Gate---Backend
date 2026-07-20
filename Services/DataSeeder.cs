using EventBoard.Api.Data;
using EventBoard.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EventBoard.Api.Services;

public static class DataSeeder
{
    public static async Task SeedAsync(AppDbContext context, IConfiguration configuration)
    {
        var adminEmail = configuration["AdminSeed:Email"] ?? "admin@eventboard.com";
        var adminPassword = configuration["AdminSeed:Password"] ?? "Admin123!";
        var adminName = configuration["AdminSeed:Name"] ?? "Admin";

        var admin = await context.Users.FirstOrDefaultAsync(u => u.Email == adminEmail);
        if (admin is null)
        {
            admin = new User
            {
                Name = adminName,
                Email = adminEmail,
                Role = UserRoles.Admin,
                IsActive = true
            };
            var hasher = new PasswordHasher<User>();
            admin.PasswordHash = hasher.HashPassword(admin, adminPassword);
            context.Users.Add(admin);
            await context.SaveChangesAsync();
        }
        else if (admin.Role != UserRoles.Admin)
        {
            admin.Role = UserRoles.Admin;
            await context.SaveChangesAsync();
        }

        if (!await context.Events.AnyAsync())
        {
            context.Events.AddRange(
                new Event
                {
                    Title = "Community Tech Meetup",
                    Description = "Monthly meetup for local developers to share what they're building.",
                    Date = DateTime.UtcNow.AddDays(7),
                    Location = "London,GB",
                    Category = "Technology",
                    UserId = admin.Id
                },
                new Event
                {
                    Title = "Summer Park Picnic",
                    Description = "Bring a dish, meet your neighbours, enjoy the sun.",
                    Date = DateTime.UtcNow.AddDays(14),
                    Location = "Manchester,GB",
                    Category = "Social",
                    UserId = admin.Id
                });

            await context.SaveChangesAsync();
        }
    }
}
