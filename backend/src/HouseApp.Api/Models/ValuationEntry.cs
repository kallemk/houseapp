namespace HouseApp.Api.Models;

public class ValuationEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    // Partition key — groups all valuations for a property together.
    public required string PropertyId { get; set; }

    public DateOnly Date { get; set; }
    public decimal Value { get; set; }
    public string? Source { get; set; }
    public string? Notes { get; set; }
    public required string CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
