using System.Text.Json.Serialization;

namespace HouseApp.Api.Models;

// All of these mirror DocumentCategory: English values (the wire contract — never rename them to
// Swedish), serialized as strings rather than integers. Swedish display labels live in
// frontend/src/utils/labels.ts.

/// <summary>
/// What kind of work a project is — the classification the dashboard totals split on.
///
/// **Append only.** There is no HasConversion for Project.WorkType in AppDbContext, so EF stores it
/// as the underlying integer; the string values are the HTTP wire contract, not what's on disk.
/// Reordering or inserting silently reclassifies every project already stored.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum WorkType
{
    /// <summary>Preserve or replace what's already there.</summary>
    Maintenance,

    /// <summary>Improve what's already there.</summary>
    Renovation,

    /// <summary>Add something new to the property.</summary>
    Investment,

    /// <summary>
    /// Movable property or equipment — a mower, furniture, tools.
    ///
    /// Budgeted like the rest, but deliberately **excluded from "Mot insatt kapital"** on the
    /// dashboard: it buys things that can leave with you, so it doesn't go into the building's
    /// value the way Renovation and Investment do. It also doesn't extend a component's life, so it
    /// stays out of the maintenance schedule for the same reason Investment does.
    /// </summary>
    Purchase,
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ProjectStatus
{
    Planned,
    InProgress,
    Completed,
    OnHold,
    Cancelled,
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ProjectPriority
{
    Low,
    Medium,
    High,
    Critical,
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CostType
{
    Materials,
    Labor,
    Tools,
    Permits,
    Other,
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PropertyType
{
    House,
    Apartment,
    Townhouse,
    Cottage,
    Other,
}
