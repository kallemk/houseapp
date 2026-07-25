using System.Text.Json.Serialization;

namespace HouseApp.Api.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DocumentCategory
{
    Deed,
    Warranty,
    Receipt,
    Photo,
    Other,
}
