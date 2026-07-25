namespace HouseApp.Api.Models;

public class Property
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public required string Nickname { get; set; }
    public required string Address { get; set; }
    public DateOnly PurchaseDate { get; set; }
    public decimal PurchasePrice { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
