namespace HouseApp.Api.Dtos.RenovationTypes;

public record RenovationTypeDto(string Id, string Name, int? RecommendedIntervalMonths);

public record CreateRenovationTypeRequest(string Name, int? RecommendedIntervalMonths);

public record UpdateRenovationTypeRequest(string Name, int? RecommendedIntervalMonths);
