using System.Text.Json.Serialization;

namespace HouseApp.Api.Models;

/// <summary>
/// **Only ever append to this list.** There is no HasConversion for Document.Category in
/// AppDbContext, so EF stores it as the underlying integer — inserting a value in the middle would
/// silently shift every later one and relabel documents already in Cosmos (every Photo becoming
/// Other, and so on). The JsonStringEnumConverter only affects the HTTP wire, not what's stored.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DocumentCategory
{
    Deed,
    Warranty,
    Receipt,
    Photo,
    Other,
    Invoice,
}
