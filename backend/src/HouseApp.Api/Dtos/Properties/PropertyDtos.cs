using HouseApp.Api.Models;

namespace HouseApp.Api.Dtos.Properties;

public record PropertyDto(
    string Id,
    string Nickname,
    string Address,
    string? Address2,
    string? PostalCode,
    string? City,
    string? Country,
    string? PropertyDesignation,
    int? YearBuilt,
    PropertyType? Type,
    double? Latitude,
    double? Longitude,
    DateOnly PurchaseDate,
    decimal PurchasePrice,
    /// <summary>The shared sandbox — visible and editable to every signed-in user.</summary>
    bool IsDemo,
    /// <summary>
    /// Whether the caller actually belongs to this property, as opposed to merely being allowed in
    /// because it's the demo. Drives whether the UI offers sharing and deletion.
    /// </summary>
    bool IsMember,
    DateTimeOffset CreatedAt);

public record PropertyMemberDto(string UserId, string Email, string DisplayName);

public record AddPropertyMemberRequest(string UserId);

public record SetDemoPropertyRequest(bool IsDemo);

/// <summary>Same shape for create and update — a property is small enough to write whole.</summary>
public record SavePropertyRequest(
    string Nickname,
    string Address,
    string? Address2,
    string? PostalCode,
    string? City,
    string? Country,
    string? PropertyDesignation,
    int? YearBuilt,
    PropertyType? Type,
    double? Latitude,
    double? Longitude,
    DateOnly PurchaseDate,
    decimal PurchasePrice);
