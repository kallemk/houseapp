using HouseApp.Api.Data;
using HouseApp.Api.Data.Migration;
using HouseApp.Api.Data.Seed;
using HouseApp.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HouseApp.Api.Tests;

/// <summary>
/// The migration runs once against real production data with no undo, so these cover the ways it
/// could quietly lose or duplicate something rather than just the happy path.
/// </summary>
public class ProjectMigratorTests
{
    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"migrator-{Guid.NewGuid()}")
            .Options);

    private static Task MigrateAsync(AppDbContext db) =>
        ProjectMigrator.MigrateAsync(db, NullLogger.Instance);

    private static LegacyRenovationEntry LegacyEntry(
        string typeId,
        string title = "Nytt tak",
        decimal amount = 50000m,
        string? vendor = null,
        string? id = null) =>
        new()
        {
            Id = id ?? Guid.NewGuid().ToString(),
            PropertyId = "property-1",
            Date = new DateOnly(2024, 3, 15),
            RenovationTypeId = typeId,
            Title = title,
            Description = "Beskrivning",
            Amount = amount,
            Vendor = vendor,
            CreatedByUserId = "user-1",
            CreatedAt = new DateTimeOffset(2024, 3, 16, 8, 0, 0, TimeSpan.Zero),
        };

    [Fact]
    public async Task Migrate_PreservesIdsSoLinkedDocumentsStillResolve()
    {
        // Document.ProjectId still holds the old entry ids — regenerating them here would orphan
        // every document attached to a renovation.
        using var db = CreateDb();
        var entry = LegacyEntry("Renovation");
        db.LegacyRenovationEntries.Add(entry);
        await db.SaveChangesAsync();

        await MigrateAsync(db);

        var project = Assert.Single(await db.Projects.ToListAsync());
        Assert.Equal(entry.Id, project.Id);
        Assert.Equal("property-1", project.PropertyId);
        Assert.Equal("user-1", project.CreatedByUserId);
        Assert.Equal(entry.CreatedAt, project.CreatedAt);
    }

    [Fact]
    public async Task Migrate_TurnsTheAmountIntoACostRowAndTheVendorIntoAContractor()
    {
        using var db = CreateDb();
        db.LegacyRenovationEntries.Add(LegacyEntry("Renovation", amount: 82000m, vendor: "Tak AB"));
        await db.SaveChangesAsync();

        await MigrateAsync(db);

        var project = Assert.Single(await db.Projects.ToListAsync());
        var cost = Assert.Single(project.Costs);
        Assert.Equal(82000m, cost.Amount);
        Assert.Equal(CostType.Other, cost.Type);
        Assert.Equal(ProjectMigrator.MigratedCostDescription, cost.Description);
        Assert.Equal(82000m, project.ActualCost);
        Assert.Equal("Tak AB", project.Contractor!.Name);
    }

    [Fact]
    public async Task Migrate_WithoutVendor_LeavesContractorNull()
    {
        using var db = CreateDb();
        db.LegacyRenovationEntries.Add(LegacyEntry("Renovation", vendor: null));
        await db.SaveChangesAsync();

        await MigrateAsync(db);

        Assert.Null((await db.Projects.ToListAsync()).Single().Contractor);
    }

    [Theory]
    [InlineData("Renovation", WorkType.Renovation)]
    [InlineData("Maintenance", WorkType.Maintenance)]
    [InlineData("Furniture", WorkType.Investment)]
    [InlineData("Other", WorkType.Maintenance)]
    public async Task Migrate_MapsSeededTypesToWorkType(string legacyTypeId, WorkType expected)
    {
        using var db = CreateDb();
        db.LegacyRenovationEntries.Add(LegacyEntry(legacyTypeId));
        await db.SaveChangesAsync();

        await MigrateAsync(db);

        var project = (await db.Projects.ToListAsync()).Single();
        Assert.Equal(expected, project.WorkType);
        Assert.Null(project.Notes);
    }

    [Fact]
    public async Task Migrate_RecordsAdminCreatedTypeNamesInNotes()
    {
        // A custom type was a kind of work with no equivalent in WorkType. Rather than silently
        // dropping what it was called, the name survives in Notes so it can be reclassified.
        using var db = CreateDb();
        db.LegacyRenovationTypes.Add(new LegacyRenovationType { Id = "custom-1", Name = "Trädgård" });
        db.LegacyRenovationEntries.Add(LegacyEntry("custom-1"));
        await db.SaveChangesAsync();

        await MigrateAsync(db);

        var project = (await db.Projects.ToListAsync()).Single();
        Assert.Equal(WorkType.Renovation, project.WorkType);
        Assert.Contains("Trädgård", project.Notes);
    }

    [Fact]
    public async Task Migrate_MarksEntriesAsCompletedWorkOnTheirOriginalDate()
    {
        using var db = CreateDb();
        db.LegacyRenovationEntries.Add(LegacyEntry("Renovation"));
        await db.SaveChangesAsync();

        await MigrateAsync(db);

        var project = (await db.Projects.ToListAsync()).Single();
        Assert.Equal(ProjectStatus.Completed, project.Status);
        Assert.Equal(new DateOnly(2024, 3, 15), project.CompletedDate);
        Assert.Equal(new DateOnly(2024, 3, 15), project.ActualStartDate);
        Assert.Equal(PropertyComponentSeeder.OtherComponentId, project.ComponentId);
    }

    [Fact]
    public async Task Migrate_RunTwice_DoesNotDuplicate()
    {
        using var db = CreateDb();
        db.LegacyRenovationEntries.Add(LegacyEntry("Renovation"));
        await db.SaveChangesAsync();

        await MigrateAsync(db);
        await MigrateAsync(db);

        Assert.Single(await db.Projects.ToListAsync());
    }

    /// <summary>
    /// The reason the guard is "copy what's missing" rather than "skip if the target is non-empty":
    /// EF Cosmos writes items individually and non-transactionally, so a crash partway through
    /// leaves a partial copy that an emptiness check would skip forever.
    /// </summary>
    [Fact]
    public async Task Migrate_CompletesAPartiallyCopiedRun()
    {
        using var db = CreateDb();
        var alreadyCopied = LegacyEntry("Renovation", title: "Redan kopierad");
        var notYetCopied = LegacyEntry("Maintenance", title: "Ej kopierad");
        db.LegacyRenovationEntries.AddRange(alreadyCopied, notYetCopied);
        db.Projects.Add(new Project
        {
            Id = alreadyCopied.Id,
            PropertyId = alreadyCopied.PropertyId,
            Name = alreadyCopied.Title,
            ComponentId = PropertyComponentSeeder.OtherComponentId,
            CreatedByUserId = "user-1",
        });
        await db.SaveChangesAsync();

        await MigrateAsync(db);

        var projects = await db.Projects.ToListAsync();
        Assert.Equal(2, projects.Count);
        Assert.Contains(projects, p => p.Name == "Ej kopierad");
    }

    [Fact]
    public async Task Migrate_WithNothingToCopy_DoesNothing()
    {
        using var db = CreateDb();

        await MigrateAsync(db);

        Assert.Empty(await db.Projects.ToListAsync());
    }

    [Fact]
    public async Task ComponentSeeder_AfterMigration_StillSeedsAndLeavesProjectsAlone()
    {
        // The two write to different containers, so ordering is not load-bearing today — this
        // pins that down so a future change that makes them overlap fails loudly.
        using var db = CreateDb();
        db.LegacyRenovationEntries.Add(LegacyEntry("Renovation"));
        await db.SaveChangesAsync();

        await MigrateAsync(db);
        await PropertyComponentSeeder.SeedAsync(db, NullLogger.Instance);

        Assert.Single(await db.Projects.ToListAsync());
        var components = await db.PropertyComponents.ToListAsync();
        Assert.Contains(components, c => c.Id == PropertyComponentSeeder.OtherComponentId);
        Assert.Contains(components, c => c.Id == "Roof");
    }
}
