using HouseApp.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace HouseApp.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<ApplicationUser> Users => Set<ApplicationUser>();
    public DbSet<Property> Properties => Set<Property>();
    public DbSet<ValuationEntry> ValuationEntries => Set<ValuationEntry>();
    public DbSet<RenovationEntry> RenovationEntries => Set<RenovationEntry>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<RenovationType> RenovationTypes => Set<RenovationType>();

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

        modelBuilder.Entity<RenovationEntry>(b =>
        {
            b.ToContainer("renovationEntries");
            b.HasPartitionKey(r => r.PropertyId);
            b.HasNoDiscriminator();
            b.Property(r => r.PropertyId).ToJsonProperty("propertyId");
            // Keeps reading old enum values ("Renovation", "Maintenance", ...) as the new
            // RenovationTypeId string — see the comment on RenovationEntry.RenovationTypeId.
            b.Property(r => r.RenovationTypeId).ToJsonProperty("Category");
        });

        modelBuilder.Entity<Document>(b =>
        {
            b.ToContainer("documents");
            b.HasPartitionKey(d => d.PropertyId);
            b.HasNoDiscriminator();
            b.Property(d => d.PropertyId).ToJsonProperty("propertyId");
        });

        modelBuilder.Entity<RenovationType>(b =>
        {
            b.ToContainer("renovationTypes");
            b.HasPartitionKey(t => t.Id);
            b.HasNoDiscriminator();
        });
    }
}
