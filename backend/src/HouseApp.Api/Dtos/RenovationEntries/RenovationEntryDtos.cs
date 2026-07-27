namespace HouseApp.Api.Dtos.RenovationEntries;

public record RenovationEntryDto(
    string Id,
    string PropertyId,
    DateOnly Date,
    string RenovationTypeId,
    string Title,
    string? Description,
    decimal Amount,
    string? Vendor,
    string CreatedByUserId,
    DateTimeOffset CreatedAt);

public record CreateRenovationEntryRequest(
    DateOnly Date,
    string RenovationTypeId,
    string Title,
    string? Description,
    decimal Amount,
    string? Vendor);

public record UpdateRenovationEntryRequest(
    DateOnly Date,
    string RenovationTypeId,
    string Title,
    string? Description,
    decimal Amount,
    string? Vendor);
