using HouseApp.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace HouseApp.Api.Data.Seed;

/// <summary>
/// Seeds default renovation types — once, ever, on the very first run when the container is
/// completely empty. Uses the same Id strings the old RenovationCategory enum used
/// ("Renovation", "Maintenance", "Furniture", "Other") purely so existing RenovationEntry rows
/// (which still hold those strings) resolve to a real type with zero data migration. After the
/// first run this never touches the container again, even if the admin later deletes one of
/// these — re-adding a type the user deliberately removed would be a bug, not a feature.
/// </summary>
public static class RenovationTypeSeeder
{
    public static async Task SeedAsync(AppDbContext db, ILogger logger)
    {
        var existing = await db.RenovationTypes.ToListAsync();
        if (existing.Count > 0)
        {
            return;
        }

        RenovationType[] defaults =
        [
            new() { Id = "Renovation", Name = "Renovering", RecommendedIntervalMonths = 180 },
            new() { Id = "Maintenance", Name = "Underhåll", RecommendedIntervalMonths = 12 },
            new() { Id = "Furniture", Name = "Möbler", RecommendedIntervalMonths = 120 },
            new() { Id = "Other", Name = "Övrigt", RecommendedIntervalMonths = null },
        ];

        db.RenovationTypes.AddRange(defaults);
        await db.SaveChangesAsync();
        logger.LogInformation("Seeded {Count} default renovation types", defaults.Length);
    }
}
