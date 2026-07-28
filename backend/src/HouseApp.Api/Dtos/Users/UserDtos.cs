namespace HouseApp.Api.Dtos.Users;

/// <summary>Never carries PasswordHash — HasPassword just tells the UI whether they can sign in with one.</summary>
public record UserDto(string Id, string Email, string DisplayName, bool HasPassword, bool IsAdmin, DateTimeOffset CreatedAt);

/// <summary>
/// A blank InitialPassword means "Google sign-in only" — no password is set. New users are always
/// regular: promoting is a separate, deliberate act on the users page.
/// </summary>
public record CreateUserRequest(string Email, string DisplayName, string? InitialPassword);

public record UpdateUserRequest(string DisplayName, bool IsAdmin);
