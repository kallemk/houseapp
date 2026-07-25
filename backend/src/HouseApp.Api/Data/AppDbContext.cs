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
            b.HasIndex(u => u.Email);
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
