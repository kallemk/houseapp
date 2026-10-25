using System.Security.Claims;
using System.Text.Encodings.Web;
using HouseApp.Api.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace HouseApp.Api.Mcp;

/// <summary>
/// Authenticates Claude's bearer tokens on <c>/mcp</c> — and only there; <c>/api</c> stays
/// cookie-only (see AddHouseAppMcp).
///
/// Every request point-reads the user, exactly as the cookie's OnValidatePrincipal does, so blocking
/// a user, deleting them or disconnecting a Claude connection all take effect on the next call
/// rather than when the hour-long access token runs out.
/// </summary>
public class McpBearerHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    McpTokenService tokens,
    AppDbContext db) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "McpBearer";
    public const string GrantClaim = "mcp_grant";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers.Authorization.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.NoResult();
        }

        var token = tokens.ReadAccessToken(header["Bearer ".Length..].Trim());
        if (token is null || token.Resource != McpOAuth.Resource(Request))
        {
            return AuthenticateResult.Fail("Invalid or expired token.");
        }

        var user = await db.Users.FindAsync(token.UserId);
        if (user is null || user.IsBlocked || user.McpGrants?.Any(g => g.Id == token.GrantId) != true)
        {
            return AuthenticateResult.Fail("The connection has been revoked.");
        }

        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Name, user.DisplayName),
                new Claim(GrantClaim, token.GrantId),
            ],
            SchemeName);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }

    /// <summary>
    /// The 401 that starts Claude's sign-in. Claude needs both the status (it ignores the header on a
    /// 200) and the resource_metadata pointer, which is how it finds the authorization server.
    /// </summary>
    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.Headers.WWWAuthenticate =
            $"Bearer resource_metadata=\"{McpOAuth.Origin(Request)}{McpOAuth.ProtectedResourceMetadataPath}\", scope=\"{McpOAuth.Scope}\"";
        return Task.CompletedTask;
    }
}
