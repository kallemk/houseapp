using HouseApp.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace HouseApp.Api.Data.Seed;

/// <summary>
/// Seeds the default parts of a house — once, ever, on the very first run when the container is
/// empty. After that it never touches the container again, even if a component is deleted:
/// re-adding something the admin deliberately removed would be a bug, not idempotent seeding.
///
/// Ids are the English names rather than GUIDs so stored references stay readable, matching what
/// RenovationTypeSeeder did. Runs *after* ProjectMigrator — see Program.cs.
/// </summary>
public static class PropertyComponentSeeder
{
    /// <summary>Fallback component: what migrated projects get, since the old model had no notion of one.</summary>
    public const string OtherComponentId = "Other";

    public static async Task SeedAsync(AppDbContext db, ILogger logger)
    {
        var existing = await db.PropertyComponents.ToListAsync();
        if (existing.Count > 0)
        {
            return;
        }

        PropertyComponent[] defaults =
        [
            new() { Id = "Roof", Name = "Tak", RecommendedIntervalMonths = 360 },
            new() { Id = "Facade", Name = "Fasad", RecommendedIntervalMonths = 144 },
            new() { Id = "Foundation", Name = "Grund", RecommendedIntervalMonths = null },
            new() { Id = "Windows", Name = "Fönster", RecommendedIntervalMonths = 240 },
            new() { Id = "Doors", Name = "Dörrar", RecommendedIntervalMonths = 240 },
            new() { Id = "Heating", Name = "Värme", RecommendedIntervalMonths = 180 },
            new() { Id = "Plumbing", Name = "VVS", RecommendedIntervalMonths = 300 },
            new() { Id = "Electrical", Name = "El", RecommendedIntervalMonths = 360 },
            new() { Id = "Interior", Name = "Interiör", RecommendedIntervalMonths = 120 },
            new() { Id = "Kitchen", Name = "Kök", RecommendedIntervalMonths = 240 },
            new() { Id = "Bathroom", Name = "Badrum", RecommendedIntervalMonths = 240 },
            new() { Id = "Insulation", Name = "Isolering", RecommendedIntervalMonths = null },
            new() { Id = "Drainage", Name = "Dränering", RecommendedIntervalMonths = 480 },
            new() { Id = OtherComponentId, Name = "Övrigt", RecommendedIntervalMonths = null },
        ];

        db.PropertyComponents.AddRange(defaults);
        await db.SaveChangesAsync();
        logger.LogInformation("Seeded {Count} default property components", defaults.Length);
    }
}
