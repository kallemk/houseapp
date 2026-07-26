using HouseApp.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HouseApp.Api.Data.Seed;

public record SeedUser(string Email, string DisplayName, string TempPassword);

/// <summary>
/// Creates the two known accounts on first run from config (Seed:Users). There is deliberately no
/// public registration endpoint — this app will only ever have these two accounts.
/// </summary>
public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db, IConfiguration configuration, ILogger logger)
    {
        var seedUsers = configuration.GetSection("Seed:Users").Get<SeedUser[]>() ?? [];
        if (seedUsers.Length == 0)
        {
            logger.LogWarning("No Seed:Users configured — no accounts will exist until one is added manually.");
            return;
        }

        var hasher = new PasswordHasher<ApplicationUser>();

        foreach (var seedUser in seedUsers)
        {
            var exists = await db.Users.AnyAsync(u => u.Email == seedUser.Email);
            if (exists)
            {
                continue;
            }

            var user = new ApplicationUser
            {
                Email = seedUser.Email,
                DisplayName = seedUser.DisplayName,
                PasswordHash = string.Empty,
            };
            user.PasswordHash = hasher.HashPassword(user, seedUser.TempPassword);

            db.Users.Add(user);
            logger.LogInformation("Seeded account for {Email}", seedUser.Email);
        }

        await db.SaveChangesAsync();
    }
}
