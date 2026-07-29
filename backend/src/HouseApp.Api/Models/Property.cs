namespace HouseApp.Api.Models;

public class Property
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public required string Nickname { get; set; }
    public required string Address { get; set; }

    // All nullable on purpose: properties created before these fields existed have no such JSON
    // property, which deserializes to the CLR default. A nullable Type shows as "—" in the UI
    // rather than silently claiming every pre-existing property is a House.
    public string? Address2 { get; set; }
    public string? PostalCode { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }

    /// <summary>Fastighetsbeteckning — the official Swedish property designation.</summary>
    public string? PropertyDesignation { get; set; }

    public int? YearBuilt { get; set; }
    public PropertyType? Type { get; set; }

    public DateOnly PurchaseDate { get; set; }
    public decimal PurchasePrice { get; set; }

    // Nullable, not just an empty-by-default list: properties created before this field existed
    // have no such JSON property at all, which Cosmos deserializes as null rather than "[]" — EF
    // Core's "required collection" write-time check would otherwise reject that, and it's also
    // simply the honest type for data that predates the field. Always use PropertiesController's
    // null-safe IsMember() helper rather than calling .Contains() on this directly.
    //
    // Every account (there are only ever 2, admin-seeded) is connected automatically when a
    // property is created — see PropertiesController.Create. Filtered in application code, not
    // via a Cosmos query, to avoid array-Contains query translation entirely.
    public List<string>? MemberUserIds { get; set; } = [];

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
