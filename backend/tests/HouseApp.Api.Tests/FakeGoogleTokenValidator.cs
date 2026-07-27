using HouseApp.Api.Services;

namespace HouseApp.Api.Tests;

/// <summary>
/// Stands in for the real call to Google so the rest of the sign-in path — allowlist lookup,
/// SignInAsync, cookie issuance — runs for real under test. The "credential" is just the email,
/// optionally prefixed to simulate the failure modes:
///   "unverified:foo@example.com" => a valid token whose email Google hasn't verified
///   "invalid"                    => a token that fails validation outright
/// </summary>
public class FakeGoogleTokenValidator : IGoogleTokenValidator
{
    public Task<GoogleUserInfo?> ValidateAsync(string credential)
    {
        if (credential == "invalid")
        {
            return Task.FromResult<GoogleUserInfo?>(null);
        }

        var emailVerified = !credential.StartsWith("unverified:", StringComparison.Ordinal);
        var email = emailVerified ? credential : credential["unverified:".Length..];

        return Task.FromResult<GoogleUserInfo?>(
            new GoogleUserInfo($"google-sub-{email}", email, emailVerified, "Google User"));
    }
}
