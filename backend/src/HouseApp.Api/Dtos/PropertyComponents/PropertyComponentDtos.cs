using System.Text.Json.Serialization;

namespace HouseApp.Api.Dtos.PropertyComponents;

public record PropertyComponentDto(string Id, string Name, int? RecommendedIntervalMonths);

public record CreatePropertyComponentRequest(string Name, int? RecommendedIntervalMonths);

public record UpdatePropertyComponentRequest(string Name, int? RecommendedIntervalMonths);

// --- Per-property component set -------------------------------------------------------------

/// <summary>
/// How one of a property's components compares to the central registry. Computed on every read by
/// comparing against the live central list, never stored — a stored copy of "is this changed?" would
/// go stale the moment either side was edited, the same reason Project.ActualCost isn't stored.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ComponentOrigin
{
    /// <summary>Identical to the central component with the same id.</summary>
    Central,

    /// <summary>Came from central, but the name or interval has been changed here.</summary>
    Modified,

    /// <summary>
    /// No central component with this id — either added for this property, or removed from the
    /// central registry since it was copied. Both mean the same thing going forward: it exists here
    /// and nowhere else, so a sync leaves it alone.
    /// </summary>
    Local,
}

public record PropertyLocalComponentDto(
    string Id,
    string Name,
    int? RecommendedIntervalMonths,
    ComponentOrigin Origin,
    /// <summary>What central says, so the UI can show what a Modified row differs from. Null unless Modified.</summary>
    string? CentralName,
    int? CentralIntervalMonths);

public record PropertyComponentSetDto(
    /// <summary>False while the property still follows the central registry — nothing has been changed here yet.</summary>
    bool Customized,
    List<PropertyLocalComponentDto> Components,
    /// <summary>Central components this property doesn't have, which a sync would add. Always 0 when not customised.</summary>
    int AvailableFromCentralCount);

public record SavePropertyLocalComponentRequest(string Name, int? RecommendedIntervalMonths);
