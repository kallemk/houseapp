namespace HouseApp.Api.Services;

/// <summary>The subset of Google's ID token payload this app cares about.</summary>
public record GoogleUserInfo(string Subject, string Email, bool EmailVerified, string? Name);

/// <summary>
/// Behind an interface purely so tests can swap in a fake (see FakeGoogleTokenValidator), the same
/// way IBlobStorageService is handled — that keeps the whole real sign-in path under test while
/// stubbing only the outbound call to Google.
/// </summary>
public interface IGoogleTokenValidator
{
    /// <summary>Validates a Google ID token ("credential"), returning null if it isn't valid.</summary>
    Task<GoogleUserInfo?> ValidateAsync(string credential);
}
