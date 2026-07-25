using HouseApp.Api.Models;

namespace HouseApp.Api.Dtos.RenovationEntries;

public record RenovationEntryDto(
    string Id,
    string PropertyId,
    DateOnly Date,
    RenovationCategory Category,
    string Title,
    string? Description,
    decimal Amount,
    string? Vendor,
    string CreatedByUserId,
    DateTimeOffset CreatedAt);

public record CreateRenovationEntryRequest(
    DateOnly Date,
    RenovationCategory Category,
    string Title,
    string? Description,
    decimal Amount,
    string? Vendor);

public record UpdateRenovationEntryRequest(
    DateOnly Date,
    RenovationCategory Category,
    string Title,
    string? Description,
    decimal Amount,
    string? Vendor);
