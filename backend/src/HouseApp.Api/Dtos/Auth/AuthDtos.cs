namespace HouseApp.Api.Dtos.Auth;

public record LoginRequest(string Email, string Password);

/// <summary>The ID token ("credential") returned by Google Identity Services in the browser.</summary>
public record GoogleLoginRequest(string Credential);

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public record MeResponse(string Id, string Email, string DisplayName);
