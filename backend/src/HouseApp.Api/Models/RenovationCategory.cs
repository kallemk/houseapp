using System.Text.Json.Serialization;

namespace HouseApp.Api.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RenovationCategory
{
    Renovation,
    Maintenance,
    Furniture,
    Other,
}
