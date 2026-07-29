namespace HouseApp.Api.Dtos.PropertyComponents;

public record PropertyComponentDto(string Id, string Name, int? RecommendedIntervalMonths);

public record CreatePropertyComponentRequest(string Name, int? RecommendedIntervalMonths);

public record UpdatePropertyComponentRequest(string Name, int? RecommendedIntervalMonths);
