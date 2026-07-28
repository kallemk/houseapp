using Google.Apis.Auth;

namespace HouseApp.Api.Services;

/// <summary>
/// Verifies Google ID tokens against Google's published public keys. GoogleJsonWebSignature checks
/// the signature, issuer, expiry, and — because Audience is set — that the token was actually
/// issued for this app's client id rather than some other site's.
/// </summary>
public class GoogleTokenValidator(IConfiguration configuration, ILogger<GoogleTokenValidator> logger)
    : IGoogleTokenValidator
{
    private string? ClientId => configuration["Authentication:Google:ClientId"];

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ClientId);

    public async Task<GoogleUserInfo?> ValidateAsync(string credential)
    {
        var clientId = ClientId;
        if (string.IsNullOrWhiteSpace(clientId))
        {
            logger.LogWarning("Google sign-in attempted but Authentication:Google:ClientId is not configured.");
            return null;
        }

        try
        {
            var payload = await GoogleJsonWebSignature.ValidateAsync(
                credential,
                new GoogleJsonWebSignature.ValidationSettings { Audience = [clientId] });

            return new GoogleUserInfo(payload.Subject, payload.Email, payload.EmailVerified, payload.Name);
        }
        catch (InvalidJwtException ex)
        {
            // Expected for expired/tampered tokens — but also for an audience mismatch, i.e. the
            // backend's client ID differing from the one the frontend signed in with. Logged at
            // Information with the message included so that case is identifiable in the log stream.
            logger.LogInformation(ex, "Rejected an invalid Google ID token.");
            return null;
        }
    }
}
