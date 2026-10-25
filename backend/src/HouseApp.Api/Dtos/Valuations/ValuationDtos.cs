using System.ComponentModel.DataAnnotations;
namespace HouseApp.Api.Dtos.Valuations;

public record ValuationEntryDto(
    string Id,
    string PropertyId,
    DateOnly Date,
    decimal Value,
    string? Source,
    string? Notes,
    string CreatedByUserId,
    DateTimeOffset CreatedAt);

public record CreateValuationEntryRequest(DateOnly Date, decimal Value, [MaxLength(Limits.ShortText)] string? Source, [MaxLength(Limits.LongText)] string? Notes);

public record UpdateValuationEntryRequest(DateOnly Date, decimal Value, [MaxLength(Limits.ShortText)] string? Source, [MaxLength(Limits.LongText)] string? Notes);
