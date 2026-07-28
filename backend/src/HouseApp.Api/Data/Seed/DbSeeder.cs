using HouseApp.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HouseApp.Api.Data.Seed;

public record SeedUser(string Email, string DisplayName, string TempPassword);

/// <summary>
/// Creates the bootstrap accounts on first run from config (Seed:Users). There is deliberately no
/// public registration endpoint — everyone after these is invited in-app via UsersController.
/// Also guarantees that at least one admin exists, which is what makes the app recoverable.
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
        var existingUsers = await db.Users.ToListAsync();
        var existingEmails = existingUsers.Select(u => u.Email).ToHashSet();

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
                // Bootstrap accounts are admins — otherwise a fresh deployment has nobody able to
                // invite anyone or manage renovation types.
                IsAdmin = true,
            };
            user.PasswordHash = hasher.HashPassword(user, seedUser.TempPassword);

            db.Users.Add(user);
            logger.LogInformation("Seeded admin account for {Email}", seedUser.Email);
        }

        // Lockout guard for databases that predate IsAdmin: a missing JSON property deserializes to
        // false (see the tolerant-read rule in CLAUDE.md), so every existing account came back as a
        // regular user and nothing in the app could ever promote one — the seeding loop above skips
        // them precisely because they already exist. Only fires when there is no admin at all, so
        // it's inert from the first successful startup onward, and doubles as a permanent recovery
        // path if the admins are ever all lost.
        if (existingUsers.Count > 0 && !existingUsers.Any(u => u.IsAdmin))
        {
            foreach (var user in existingUsers)
            {
                user.IsAdmin = true;
            }

            logger.LogWarning(
                "No admin accounts found — promoted all {Count} existing accounts to admin.",
                existingUsers.Count);
        }

        await db.SaveChangesAsync();
    }
}
