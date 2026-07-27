namespace HouseApp.Api.Models;

/// <summary>
/// Admin-managed replacement for what used to be a hardcoded enum — lets the two users define
/// whatever renovation/maintenance types are relevant to them, each with an optional recommended
/// interval (e.g. "repaint exterior every 96 months") for future use in reminders.
/// </summary>
public class RenovationType
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public required string Name { get; set; }
    public int? RecommendedIntervalMonths { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
