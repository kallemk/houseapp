namespace HouseApp.Api.Data.Migration;

// Read-only stand-ins for the pre-project model, mapped to the old renovationEntries/renovationTypes
// containers. They exist purely so ProjectMigrator can read what's already in Cosmos — nothing else
// in the app should touch them.
//
// DELETE THESE once the project model has been running in production long enough that rolling back
// is off the table. Removing them also removes the last reason to keep the old containers.

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
