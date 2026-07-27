namespace HouseApp.Api.Dtos.Users;

/// <summary>Never carries PasswordHash — HasPassword just tells the UI whether they can sign in with one.</summary>
public record UserDto(string Id, string Email, string DisplayName, bool HasPassword, DateTimeOffset CreatedAt);

/// <summary>A blank InitialPassword means "Google sign-in only" — no password is set.</summary>
public record CreateUserRequest(string Email, string DisplayName, string? InitialPassword);

public record UpdateUserRequest(string DisplayName);
