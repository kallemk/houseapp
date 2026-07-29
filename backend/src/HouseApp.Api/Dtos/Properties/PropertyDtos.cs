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
    DateOnly PurchaseDate,
    decimal PurchasePrice,
    DateTimeOffset CreatedAt);

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
    DateOnly PurchaseDate,
    decimal PurchasePrice);
