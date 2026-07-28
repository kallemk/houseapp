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
    /// <summary>
    /// False when no Google client ID is configured, i.e. Google sign-in isn't set up on this
    /// deployment. Kept separate from ValidateAsync so the API can answer "the server isn't
    /// configured" (an ops problem) differently from "your token is bad" (an auth problem) —
    /// conflating the two as a bare 401 makes a misconfigured deployment very hard to diagnose.
    /// </summary>
    bool IsConfigured { get; }

    /// <summary>Validates a Google ID token ("credential"), returning null if it isn't valid.</summary>
    Task<GoogleUserInfo?> ValidateAsync(string credential);
}
