namespace HouseApp.Api.Models;

public class RenovationEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    // Partition key — groups all renovation entries for a property together.
    public required string PropertyId { get; set; }

    public DateOnly Date { get; set; }

    // Was a RenovationCategory enum — now a free-form reference to RenovationType.Id, managed via
    // RenovationTypesController instead of hardcoded in code. Mapped to the JSON property "Category"
    // (see AppDbContext) so existing entries' enum string values ("Renovation", "Maintenance", ...)
    // keep working unchanged: RenovationTypeSeeder seeds default types using those exact strings as
    // their Id, so no data migration was needed for this rename.
    public required string RenovationTypeId { get; set; }

    public required string Title { get; set; }
    public string? Description { get; set; }
    public decimal Amount { get; set; }
    public string? Vendor { get; set; }
    public required string CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
