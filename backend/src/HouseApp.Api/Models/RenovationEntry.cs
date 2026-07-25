namespace HouseApp.Api.Models;

public class RenovationEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    // Partition key — groups all renovation entries for a property together.
    public required string PropertyId { get; set; }

    public DateOnly Date { get; set; }
    public RenovationCategory Category { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public decimal Amount { get; set; }
    public string? Vendor { get; set; }
    public required string CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
