using HouseApp.Api.Data.Seed;
using HouseApp.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace HouseApp.Api.Data.Migration;

/// <summary>
/// Copies the old renovation log into the projects container, once. Runs on every startup (like
/// DbSeeder) because App Service has no separate migration step.
///
/// Copies rather than moves: renovationEntries is left untouched and complete, which is the entire
/// rollback path if the project model misbehaves in production.
/// </summary>
public static class ProjectMigrator
{
    /// <summary>Description stamped on the single cost row each migrated entry's Amount becomes.</summary>
    public const string MigratedCostDescription = "Migrerad från renoveringslogg";

    // The four types RenovationTypeSeeder created. Anything else is an admin-created type whose
    // meaning we can't infer — see below.
    private static readonly Dictionary<string, WorkType> WorkTypeByLegacyTypeId = new()
    {
        ["Renovation"] = WorkType.Renovation,
        ["Maintenance"] = WorkType.Maintenance,
        ["Furniture"] = WorkType.Investment,
        ["Other"] = WorkType.Maintenance,
    };

    public static async Task MigrateAsync(AppDbContext db, ILogger logger)
    {
        var legacyEntries = await db.LegacyRenovationEntries.ToListAsync();
        if (legacyEntries.Count == 0)
        {
            return;
        }

        // Copy only what isn't already there, rather than guarding on "is the target empty?". EF
        // Cosmos writes items individually and non-transactionally, so a failure partway through
        // leaves a partial copy that an emptiness check would then skip forever. This way a rerun
        // finishes the job, and running twice can't duplicate anything.
        var existingIds = (await db.Projects.ToListAsync()).Select(p => p.Id).ToHashSet();
        var toMigrate = legacyEntries.Where(e => !existingIds.Contains(e.Id)).ToList();
        if (toMigrate.Count == 0)
        {
            return;
        }

        var legacyTypeNames = (await db.LegacyRenovationTypes.ToListAsync())
            .ToDictionary(t => t.Id, t => t.Name);

        foreach (var entry in toMigrate)
        {
            db.Projects.Add(ToProject(entry, legacyTypeNames));
        }

        await db.SaveChangesAsync();
        logger.LogInformation("Migrated {Count} renovation entries into projects", toMigrate.Count);
    }

    private static Project ToProject(LegacyRenovationEntry entry, IReadOnlyDictionary<string, string> legacyTypeNames)
    {
        var known = WorkTypeByLegacyTypeId.TryGetValue(entry.RenovationTypeId, out var workType);

        // An admin-created type was a kind of work, and there's no way to infer which. Rather than
        // silently dropping it, record its name in Notes so the entry can be reclassified by hand.
        var notes = known || !legacyTypeNames.TryGetValue(entry.RenovationTypeId, out var typeName)
            ? null
            : $"Tidigare typ: {typeName}";

        return new Project
        {
            // Preserved verbatim — Document.ProjectId already holds these values.
            Id = entry.Id,
            PropertyId = entry.PropertyId,
            CreatedByUserId = entry.CreatedByUserId,
            CreatedAt = entry.CreatedAt,

            Name = entry.Title,
            Description = entry.Description,
            Notes = notes,

            WorkType = known ? workType : WorkType.Renovation,
            // The old types classified the work, not the part of the house — there's nothing to
            // derive a component from, so everything lands in Övrigt to be reassigned.
            ComponentId = PropertyComponentSeeder.OtherComponentId,

            // These are historical records of work already done.
            Status = ProjectStatus.Completed,
            ActualStartDate = entry.Date,
            CompletedDate = entry.Date,
            Priority = ProjectPriority.Medium,

            EstimatedCost = entry.Amount,
            Costs =
            [
                new ProjectCost
                {
                    Type = CostType.Other,
                    Description = MigratedCostDescription,
                    Amount = entry.Amount,
                    DateIncurred = entry.Date,
                },
            ],
            Contractor = string.IsNullOrWhiteSpace(entry.Vendor) ? null : new ContractorInfo { Name = entry.Vendor },
        };
    }
}
