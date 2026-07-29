namespace HouseApp.Api.Models;

/// <summary>
/// A part of the house a project can concern (Tak, Fasad, VVS, …). Admin-managed data rather than a
/// hardcoded enum so the list can be edited in-app — the same role RenovationType used to play, and
/// it reuses the same admin-gated controller/page shape.
///
/// RecommendedIntervalMonths is unused in stage 1; it exists because the planned MaintenanceSchedule
/// derives "next due" from it plus the last completed maintenance project for the component.
/// </summary>
public class PropertyComponent
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public required string Name { get; set; }
    public int? RecommendedIntervalMonths { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
