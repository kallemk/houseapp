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
        });

        modelBuilder.Entity<RenovationEntry>(b =>
        {
            b.ToContainer("renovationEntries");
            b.HasPartitionKey(r => r.PropertyId);
            b.HasNoDiscriminator();
        });

        modelBuilder.Entity<Document>(b =>
        {
            b.ToContainer("documents");
            b.HasPartitionKey(d => d.PropertyId);
            b.HasNoDiscriminator();
        });
    }
}
