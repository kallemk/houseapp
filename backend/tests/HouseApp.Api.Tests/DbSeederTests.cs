using HouseApp.Api.Data;
using HouseApp.Api.Data.Seed;
using HouseApp.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HouseApp.Api.Tests;

/// <summary>
/// Covers the one failure mode in the admin role that can't be fixed from the UI afterwards: a
/// database in which nobody is an admin, so nobody can promote anyone. Production reaches that
/// state simply by deploying the IsAdmin field — existing user documents have no such JSON
/// property and deserialize it as false.
/// </summary>
public class DbSeederTests
{
    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"seeder-{Guid.NewGuid()}")
            .Options);

    private static IConfiguration CreateConfiguration(params string[] emails)
    {
        var values = new Dictionary<string, string?>();
        for (var i = 0; i < emails.Length; i++)
        {
            values[$"Seed:Users:{i}:Email"] = emails[i];
            values[$"Seed:Users:{i}:DisplayName"] = $"Seeded {i}";
            values[$"Seed:Users:{i}:TempPassword"] = "Secret123!";
        }

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    private static Task SeedAsync(AppDbContext db, IConfiguration configuration) =>
        DbSeeder.SeedAsync(db, configuration, NullLogger.Instance);

    [Fact]
    public async Task SeedAsync_OnEmptyDatabase_CreatesBootstrapAccountsAsAdmins()
    {
        using var db = CreateDb();

        await SeedAsync(db, CreateConfiguration("a@example.com", "b@example.com"));

        var users = await db.Users.ToListAsync();
        Assert.Equal(2, users.Count);
        Assert.All(users, u => Assert.True(u.IsAdmin));
    }

    [Fact]
    public async Task SeedAsync_WhenNobodyIsAdmin_PromotesEveryExistingAccount()
    {
        // The production-upgrade case: the seed accounts already exist (so the seeding loop skips
        // them entirely) but came back from Cosmos with IsAdmin == false.
        using var db = CreateDb();
        db.Users.AddRange(
            new ApplicationUser { Email = "a@example.com", DisplayName = "A", IsAdmin = false },
            new ApplicationUser { Email = "invited@example.com", DisplayName = "Invited", IsAdmin = false });
        await db.SaveChangesAsync();

        await SeedAsync(db, CreateConfiguration("a@example.com"));

        var users = await db.Users.ToListAsync();
        Assert.Equal(2, users.Count);
        Assert.All(users, u => Assert.True(u.IsAdmin));
    }

    [Fact]
    public async Task SeedAsync_WhenAnAdminExists_LeavesRegularUsersAlone()
    {
        // Steady state: the backfill above must not re-promote someone who was deliberately demoted.
        using var db = CreateDb();
        db.Users.AddRange(
            new ApplicationUser { Email = "a@example.com", DisplayName = "A", IsAdmin = true },
            new ApplicationUser { Email = "regular@example.com", DisplayName = "Regular", IsAdmin = false });
        await db.SaveChangesAsync();

        await SeedAsync(db, CreateConfiguration("a@example.com"));

        var regular = (await db.Users.ToListAsync()).Single(u => u.Email == "regular@example.com");
        Assert.False(regular.IsAdmin);
    }

    [Fact]
    public async Task SeedAsync_WithNoSeedUsersConfigured_DoesNotThrow()
    {
        using var db = CreateDb();

        await SeedAsync(db, CreateConfiguration());

        Assert.Empty(await db.Users.ToListAsync());
    }
}
