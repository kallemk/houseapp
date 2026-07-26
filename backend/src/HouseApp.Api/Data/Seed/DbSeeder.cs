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

        // One plain read of the whole (tiny, 2-row) container rather than a per-user existence
        // query — the Cosmos provider's SQL generation for predicate-based Any/Single/First is
        // unreliable (see the "Identifier 'root' could not be resolved" crash this replaced).
        var existingEmails = (await db.Users.ToListAsync()).Select(u => u.Email).ToHashSet();

        foreach (var seedUser in seedUsers)
        {
            if (existingEmails.Contains(seedUser.Email))
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
