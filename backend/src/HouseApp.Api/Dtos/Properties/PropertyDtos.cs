namespace HouseApp.Api.Dtos.Properties;

public record PropertyDto(
    string Id,
    string Nickname,
    string Address,
    DateOnly PurchaseDate,
    decimal PurchasePrice,
    DateTimeOffset CreatedAt);

public record CreatePropertyRequest(string Nickname, string Address, DateOnly PurchaseDate, decimal PurchasePrice);

public record UpdatePropertyRequest(string Nickname, string Address, DateOnly PurchaseDate, decimal PurchasePrice);
