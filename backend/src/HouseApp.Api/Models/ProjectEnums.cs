using System.Text.Json.Serialization;

namespace HouseApp.Api.Models;

// All of these mirror DocumentCategory: English values (the wire contract — never rename them to
// Swedish), serialized as strings rather than integers. Swedish display labels live in
// frontend/src/utils/labels.ts.

/// <summary>What kind of work a project is — the classification the dashboard totals split on.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum WorkType
{
    Maintenance,
    Renovation,
    Investment,
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
