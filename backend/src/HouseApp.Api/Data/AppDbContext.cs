using HouseApp.Api.Data.Migration;
using HouseApp.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace HouseApp.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<ApplicationUser> Users => Set<ApplicationUser>();
    public DbSet<Property> Properties => Set<Property>();
    public DbSet<ValuationEntry> ValuationEntries => Set<ValuationEntry>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<PropertyComponent> PropertyComponents => Set<PropertyComponent>();
    public DbSet<Budget> Budgets => Set<Budget>();
    // No DbSet for the maintenance schedule — it's derived, see MaintenanceScheduleController.

    // Superseded by Projects/PropertyComponents and read only by ProjectMigrator — see LegacyModels.
    public DbSet<LegacyRenovationEntry> LegacyRenovationEntries => Set<LegacyRenovationEntry>();
    public DbSet<LegacyRenovationType> LegacyRenovationTypes => Set<LegacyRenovationType>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ApplicationUser>(b =>
        {
            b.ToContainer("users");
            b.HasPartitionKey(u => u.Id);
            b.HasNoDiscriminator();
            // No HasIndex() — the Cosmos provider doesn't support EF index declarations (it indexes
            // every property automatically by default), and calling it throws at model-validation time.
        });

        modelBuilder.Entity<Property>(b =>
        {
            b.ToContainer("properties");
            b.HasPartitionKey(p => p.Id);
            b.HasNoDiscriminator();
            // Nested JSON on the property document rather than a container of its own: a property's
            // components are never read without the property, and there are only ever a handful.
            b.OwnsMany(p => p.LocalComponents);
        });

        modelBuilder.Entity<ValuationEntry>(b =>
        {
            b.ToContainer("valuationEntries");
            b.HasPartitionKey(v => v.PropertyId);
            b.HasNoDiscriminator();
            // Cosmos extracts the partition key value from the document JSON using the container's
            // declared path (infra/modules/cosmos.bicep: "/propertyId", lowercase) — EF Core only
            // special-cases the primary "id" field to lowercase, so PropertyId would otherwise be
            // written as "PropertyId" and every write would fail with PartitionKeyMismatch (extracted
            // key doesn't match the one in the request header) because the case doesn't line up.
            b.Property(v => v.PropertyId).ToJsonProperty("propertyId");
        });

        modelBuilder.Entity<Document>(b =>
        {
            b.ToContainer("documents");
            b.HasPartitionKey(d => d.PropertyId);
            b.HasNoDiscriminator();
            b.Property(d => d.PropertyId).ToJsonProperty("propertyId");
            // The documents container is not migrated, so this keeps reading the field under its
            // original name. Safe because ProjectMigrator preserves entry ids as project ids.
            b.Property(d => d.ProjectId).ToJsonProperty("RenovationEntryId");
        });

        modelBuilder.Entity<Project>(b =>
        {
            b.ToContainer("projects");
            b.HasPartitionKey(p => p.PropertyId);
            b.HasNoDiscriminator();
            b.Property(p => p.PropertyId).ToJsonProperty("propertyId");
            // Owned types => nested JSON inside the project document, not separate containers.
            b.OwnsOne(p => p.Contractor);
            b.OwnsMany(p => p.Costs);
            b.OwnsMany(p => p.Milestones);
            // ActualCost is computed from Costs; nothing to store.
            b.Ignore(p => p.ActualCost);
        });

        modelBuilder.Entity<Budget>(b =>
        {
            b.ToContainer("budgets");
            b.HasPartitionKey(x => x.PropertyId);
            b.HasNoDiscriminator();
            b.Property(x => x.PropertyId).ToJsonProperty("propertyId");
        });

        modelBuilder.Entity<PropertyComponent>(b =>
        {
            b.ToContainer("propertyComponents");
            b.HasPartitionKey(c => c.Id);
            b.HasNoDiscriminator();
        });

        // Legacy containers — read-only, for ProjectMigrator. Delete along with LegacyModels.cs.
        modelBuilder.Entity<LegacyRenovationEntry>(b =>
        {
            b.ToContainer("renovationEntries");
            b.HasPartitionKey(r => r.PropertyId);
            b.HasNoDiscriminator();
            b.Property(r => r.PropertyId).ToJsonProperty("propertyId");
            b.Property(r => r.RenovationTypeId).ToJsonProperty("Category");
        });

        modelBuilder.Entity<LegacyRenovationType>(b =>
        {
            b.ToContainer("renovationTypes");
            b.HasPartitionKey(t => t.Id);
            b.HasNoDiscriminator();
        });
    }
}
