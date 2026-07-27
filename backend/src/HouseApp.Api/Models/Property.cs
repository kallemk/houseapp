namespace HouseApp.Api.Models;

public class Property
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public required string Nickname { get; set; }
    public required string Address { get; set; }
    public DateOnly PurchaseDate { get; set; }
    public decimal PurchasePrice { get; set; }

    // Every account (there are only ever 2, admin-seeded) is connected automatically when a
    // property is created — see PropertiesController.Create. Filtered in application code, not
    // via a Cosmos query, to avoid array-Contains query translation entirely.
    public List<string> MemberUserIds { get; set; } = [];

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
