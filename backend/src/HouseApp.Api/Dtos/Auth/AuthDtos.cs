namespace HouseApp.Api.Dtos.Auth;

public record LoginRequest(string Email, string Password);

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public record MeResponse(string Id, string Email, string DisplayName);
