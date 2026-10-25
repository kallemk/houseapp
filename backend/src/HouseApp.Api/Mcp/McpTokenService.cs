using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Caching.Memory;

namespace HouseApp.Api.Mcp;

public record McpClientRegistration(List<string> RedirectUris, string ClientName);

public record McpAuthorizationRequest(
    string ClientId,
    string ClientName,
    string RedirectUri,
    string? State,
    string CodeChallenge,
    string Scope,
    string Resource);

public record McpAuthorizationCode(
    string CodeId,
    string UserId,
    string ClientId,
    string ClientName,
    string RedirectUri,
    string CodeChallenge,
    string Scope,
    string Resource);

public record McpAccessToken(string UserId, string GrantId, string Resource);

public record McpRefreshToken(string UserId, string GrantId, string TokenId, string ClientId, string Resource);

/// <summary>
/// Every OAuth artefact Claude handles is a self-contained, Data Protection-protected blob — the same
/// key ring as the session cookie and the Drive tokens. That's what lets the authorization server
/// have no container of its own: the client id *is* the registration, the code *is* the pending
/// grant. The only state is <see cref="Models.McpGrant"/> on the user, for revocation and refresh
/// reuse detection.
///
/// Each kind gets its own protector purpose, so one can never be passed off as another — a code is
/// not an access token, and an access token is not a refresh token, even though both are just
/// strings to the client. Losing the key ring invalidates all of them: every Claude connection has
/// to be made again, on top of everyone signing in again.
/// </summary>
public class McpTokenService(IDataProtectionProvider dataProtection, IMemoryCache cache)
{
    public static readonly TimeSpan AuthorizationRequestLifetime = TimeSpan.FromMinutes(10);
    public static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromHours(1);
    public static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(60);

    private readonly IDataProtector _clients = dataProtection.CreateProtector("HouseApp.Mcp.Client");
    private readonly ITimeLimitedDataProtector _requests =
        dataProtection.CreateProtector("HouseApp.Mcp.AuthorizationRequest").ToTimeLimitedDataProtector();
    private readonly ITimeLimitedDataProtector _codes =
        dataProtection.CreateProtector("HouseApp.Mcp.Code").ToTimeLimitedDataProtector();
    private readonly ITimeLimitedDataProtector _accessTokens =
        dataProtection.CreateProtector("HouseApp.Mcp.AccessToken").ToTimeLimitedDataProtector();
    private readonly ITimeLimitedDataProtector _refreshTokens =
        dataProtection.CreateProtector("HouseApp.Mcp.RefreshToken").ToTimeLimitedDataProtector();

    // The registration never expires: Claude keeps its client id for as long as the connection lives.
    public string CreateClientId(McpClientRegistration registration) =>
        _clients.Protect(JsonSerializer.Serialize(registration));

    public McpClientRegistration? ReadClientId(string clientId) => Read<McpClientRegistration>(_clients, clientId);

    public string CreateAuthorizationRequest(McpAuthorizationRequest request) =>
        _requests.Protect(JsonSerializer.Serialize(request), AuthorizationRequestLifetime);

    public McpAuthorizationRequest? ReadAuthorizationRequest(string value) => Read<McpAuthorizationRequest>(_requests, value);

    public string CreateCode(McpAuthorizationCode code) => _codes.Protect(JsonSerializer.Serialize(code), CodeLifetime);

    /// <summary>
    /// Reads a code **and spends it**: a code is single-use, so a second presentation within its five
    /// minutes is refused even with the right PKCE verifier. Remembered in memory, which is correct
    /// only while the app runs one instance — the same assumption as the file-backed Data Protection
    /// key ring, and documented with it.
    /// </summary>
    public McpAuthorizationCode? RedeemCode(string value)
    {
        var code = Read<McpAuthorizationCode>(_codes, value);
        if (code is null)
        {
            return null;
        }

        var key = $"mcp-code:{code.CodeId}";
        if (cache.TryGetValue(key, out _))
        {
            return null;
        }

        cache.Set(key, true, CodeLifetime);
        return code;
    }

    public string CreateAccessToken(McpAccessToken token) =>
        _accessTokens.Protect(JsonSerializer.Serialize(token), AccessTokenLifetime);

    public McpAccessToken? ReadAccessToken(string value) => Read<McpAccessToken>(_accessTokens, value);

    public string CreateRefreshToken(McpRefreshToken token) =>
        _refreshTokens.Protect(JsonSerializer.Serialize(token), RefreshTokenLifetime);

    public McpRefreshToken? ReadRefreshToken(string value) => Read<McpRefreshToken>(_refreshTokens, value);

    /// <summary>PKCE S256: base64url(SHA-256(verifier)) must equal the challenge sent at authorize.</summary>
    public static bool VerifyPkce(string codeVerifier, string codeChallenge)
    {
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
        var computed = WebEncoders.Base64UrlEncode(hash);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(computed),
            Encoding.ASCII.GetBytes(codeChallenge));
    }

    public static string NewId() => WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(16));

    /// <summary>Null for anything tampered with, expired, or protected for a different purpose.</summary>
    private static T? Read<T>(IDataProtector protector, string value) where T : class
    {
        try
        {
            return JsonSerializer.Deserialize<T>(protector.Unprotect(value));
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException or FormatException)
        {
            return null;
        }
    }
}
