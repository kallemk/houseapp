namespace HouseApp.Api.Data.Migration;

// Read-only stand-ins for the pre-project model, mapped to the old renovationEntries/renovationTypes
// containers, which still hold a complete pre-migration snapshot.
//
// Nothing reads them any more: the one-shot migration into `projects` has run, and ProjectMigrator
// was deleted because running it on every startup resurrected deliberately-deleted projects (see
// Program.cs). These types are kept only so the containers stay mapped and inspectable while
// rolling back is still on the table.
//
// DELETE THESE together with the two containers, once the project model has run in production long
// enough that reverting is off the table.

/// <summary>Was Models.RenovationEntry. RenovationTypeId keeps its legacy JSON name "Category".</summary>
public class LegacyRenovationEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public required string PropertyId { get; set; }
    public DateOnly Date { get; set; }
    public required string RenovationTypeId { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public decimal Amount { get; set; }
    public string? Vendor { get; set; }
    public required string CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>Was Models.RenovationType — a kind of work, which is why it maps onto WorkType and not onto PropertyComponent.</summary>
public class LegacyRenovationType
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public required string Name { get; set; }
    public int? RecommendedIntervalMonths { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
