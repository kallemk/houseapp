using HouseApp.Api.Data;
using HouseApp.Api.Extensions;
using HouseApp.Api.Mcp;
using HouseApp.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HouseApp.Api.Controllers;

/// <summary>
/// HusTracker's OAuth 2.1 authorization server, which exists for one client: Claude, connecting to
/// <c>/mcp</c>. Claude's custom connectors require OAuth with self-registration (DCR here) and PKCE,
/// so a fixed API key isn't an option for an individual's account.
///
/// It sits on top of the sign-in the app already has rather than replacing any of it: authorize sends
/// the browser to the SPA's consent page, which needs the ordinary session cookie, so a user signs in
/// exactly as always (password or Google) and then approves. What Claude ends up holding is a bearer
/// token that only <c>/mcp</c> accepts — never the cookie, and never valid on <c>/api</c>.
///
/// Stateless apart from <see cref="McpGrant"/>: see <see cref="McpTokenService"/>.
/// </summary>
[ApiController]
public class OAuthController(AppDbContext db, McpTokenService tokens, ILogger<OAuthController> logger) : ControllerBase
{
    /// <summary>A user can't accumulate connections forever: each fresh connect registers anew.</summary>
    private const int MaxGrantsPerUser = 10;

    // --- Discovery ---------------------------------------------------------------------------------

    /// <summary>
    /// RFC 9728. Served at the root and under the /mcp suffix, since clients probe both. `resource`
    /// must equal the URL the user types into Claude exactly, and Claude only ever uses the first
    /// authorization server listed.
    /// </summary>
    [HttpGet(McpOAuth.ProtectedResourceMetadataPath)]
    [HttpGet(McpOAuth.ProtectedResourceMetadataPath + McpOAuth.McpPath)]
    public IActionResult ProtectedResourceMetadata() => Ok(new
    {
        resource = McpOAuth.Resource(Request),
        authorization_servers = new[] { McpOAuth.Origin(Request) },
        scopes_supported = new[] { McpOAuth.Scope },
        bearer_methods_supported = new[] { "header" },
        resource_name = "HusTracker",
    });

    /// <summary>
    /// RFC 8414. Advertises DCR and *not* Client ID Metadata Documents: CIMD would mean fetching
    /// client-supplied URLs from the server, and Claude falls back to DCR when it isn't offered.
    /// `offline_access` is listed so Claude asks for — and gets — a refresh token.
    /// </summary>
    [HttpGet(McpOAuth.AuthorizationServerMetadataPath)]
    public IActionResult AuthorizationServerMetadata()
    {
        var origin = McpOAuth.Origin(Request);
        return Ok(new
        {
            issuer = origin,
            authorization_endpoint = $"{origin}/oauth/authorize",
            token_endpoint = $"{origin}/oauth/token",
            registration_endpoint = $"{origin}/oauth/register",
            response_types_supported = new[] { "code" },
            grant_types_supported = new[] { "authorization_code", "refresh_token" },
            code_challenge_methods_supported = new[] { "S256" },
            token_endpoint_auth_methods_supported = new[] { "none" },
            scopes_supported = new[] { McpOAuth.Scope, "offline_access" },
        });
    }

    // --- Registration (RFC 7591) ---------------------------------------------------------------------

    public record RegistrationRequest(
        [property: System.Text.Json.Serialization.JsonPropertyName("redirect_uris")] List<string>? RedirectUris,
        [property: System.Text.Json.Serialization.JsonPropertyName("client_name")] string? ClientName);

    /// <summary>
    /// Open to anyone, as DCR must be, and stateless: the client id is the registration itself,
    /// protected. What keeps it safe is the redirect allowlist — a token can only ever be delivered
    /// to Claude's own callback or to the user's own machine.
    /// </summary>
    [HttpPost("oauth/register")]
    public IActionResult Register(RegistrationRequest request)
    {
        var redirectUris = request.RedirectUris ?? [];
        if (redirectUris.Count == 0 || !redirectUris.All(McpOAuth.IsAllowedRedirectUri))
        {
            return BadRequest(new
            {
                error = "invalid_redirect_uri",
                error_description = "Only Claude's callback and loopback redirect URIs are accepted.",
            });
        }

        var clientName = string.IsNullOrWhiteSpace(request.ClientName) ? "Claude" : request.ClientName.Trim();
        if (clientName.Length > Limits.ShortText)
        {
            clientName = clientName[..Limits.ShortText];
        }

        var clientId = tokens.CreateClientId(new McpClientRegistration(redirectUris, clientName));
        return StatusCode(StatusCodes.Status201Created, new
        {
            client_id = clientId,
            client_id_issued_at = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            client_name = clientName,
            redirect_uris = redirectUris,
            token_endpoint_auth_method = "none",
            grant_types = new[] { "authorization_code", "refresh_token" },
            response_types = new[] { "code" },
        });
    }

    // --- Authorization -----------------------------------------------------------------------------

    /// <summary>
    /// Checks the request and hands it to the SPA's consent page, packed and protected so the page
    /// can't be fed a request this endpoint didn't vet.
    ///
    /// A bad client id or redirect URI is answered here and **never** redirected: until both check
    /// out, the redirect URI is exactly what an attacker would want us to send someone to. Every
    /// other problem goes back to the (now trusted) redirect URI as an OAuth error, per RFC 6749.
    /// </summary>
    [HttpGet("oauth/authorize")]
    public IActionResult Authorize(
        [FromQuery(Name = "response_type")] string? responseType,
        [FromQuery(Name = "client_id")] string? clientId,
        [FromQuery(Name = "redirect_uri")] string? redirectUri,
        [FromQuery(Name = "code_challenge")] string? codeChallenge,
        [FromQuery(Name = "code_challenge_method")] string? codeChallengeMethod,
        [FromQuery] string? state,
        [FromQuery] string? scope,
        [FromQuery] string? resource)
    {
        var client = clientId is null ? null : tokens.ReadClientId(clientId);
        if (client is null)
        {
            return BadRequest("Okänd klient. Ta bort kopplingen i Claude och lägg till den igen.");
        }

        if (redirectUri is null || !McpOAuth.MatchesRegistered(redirectUri, client.RedirectUris))
        {
            return BadRequest("Ogiltig redirect_uri för den här klienten.");
        }

        string? error = responseType != "code" ? "unsupported_response_type"
            : string.IsNullOrEmpty(codeChallenge) || codeChallengeMethod != "S256" ? "invalid_request"
            : !McpOAuth.IsThisResource(resource, Request) ? "invalid_target"
            : null;
        if (error is not null)
        {
            return Redirect(WithQuery(redirectUri, ("error", error), ("state", state)));
        }

        var packed = tokens.CreateAuthorizationRequest(new McpAuthorizationRequest(
            clientId!,
            client.ClientName,
            redirectUri,
            state,
            codeChallenge!,
            McpOAuth.Scope,
            McpOAuth.Resource(Request)));

        return Redirect($"/oauth/consent?request={Uri.EscapeDataString(packed)}");
    }

    public record ConsentSummary(string ClientName, string RedirectHost, bool IsLoopback);

    /// <summary>What the consent page shows — including where the user is about to be sent.</summary>
    [HttpGet("api/oauth/consent")]
    [Authorize]
    public ActionResult<ConsentSummary> GetConsent([FromQuery] string request)
    {
        var pending = tokens.ReadAuthorizationRequest(request);
        if (pending is null)
        {
            return BadRequest(new { message = "Begäran har gått ut. Starta kopplingen från Claude igen." });
        }

        return Ok(new ConsentSummary(
            pending.ClientName,
            new Uri(pending.RedirectUri).Host,
            McpOAuth.IsLoopback(pending.RedirectUri)));
    }

    public record ConsentDecision(string Request, bool Approve);

    public record ConsentResult(string RedirectUrl);

    /// <summary>
    /// The user's answer. Cookie-authenticated and JSON-only, like the rest of /api, so a cross-site
    /// form can't approve on someone's behalf. Approval yields a five-minute, single-use code bound to
    /// this user, the client, the redirect URI and the PKCE challenge.
    /// </summary>
    [HttpPost("api/oauth/consent")]
    [Authorize]
    public ActionResult<ConsentResult> Decide(ConsentDecision decision)
    {
        var pending = tokens.ReadAuthorizationRequest(decision.Request);
        if (pending is null)
        {
            return BadRequest(new { message = "Begäran har gått ut. Starta kopplingen från Claude igen." });
        }

        var issuer = McpOAuth.Origin(Request);
        if (!decision.Approve)
        {
            return Ok(new ConsentResult(WithQuery(pending.RedirectUri,
                ("error", "access_denied"), ("state", pending.State), ("iss", issuer))));
        }

        var code = tokens.CreateCode(new McpAuthorizationCode(
            McpTokenService.NewId(),
            User.CurrentUserId(),
            pending.ClientId,
            pending.ClientName,
            pending.RedirectUri,
            pending.CodeChallenge,
            pending.Scope,
            pending.Resource));

        return Ok(new ConsentResult(WithQuery(pending.RedirectUri,
            ("code", code), ("state", pending.State), ("iss", issuer))));
    }

    // --- Token -------------------------------------------------------------------------------------

    /// <summary>
    /// Form-encoded, as RFC 6749 requires and Claude sends. Errors are RFC 6749 codes —
    /// <c>invalid_grant</c> in particular is what tells Claude to send the user through sign-in again
    /// rather than retrying.
    /// </summary>
    [HttpPost("oauth/token")]
    public async Task<IActionResult> Token([FromForm] IFormCollection form)
    {
        Response.Headers.CacheControl = "no-store";

        return form["grant_type"].ToString() switch
        {
            "authorization_code" => await ExchangeCodeAsync(form),
            "refresh_token" => await RefreshAsync(form),
            _ => TokenError("unsupported_grant_type"),
        };
    }

    private async Task<IActionResult> ExchangeCodeAsync(IFormCollection form)
    {
        var code = tokens.RedeemCode(form["code"].ToString());
        if (code is null
            || code.ClientId != form["client_id"].ToString()
            || code.RedirectUri != form["redirect_uri"].ToString()
            || !McpOAuth.IsThisResource(NullIfEmpty(form["resource"]), Request)
            || !McpTokenService.VerifyPkce(form["code_verifier"].ToString(), code.CodeChallenge))
        {
            return TokenError("invalid_grant");
        }

        var user = await db.Users.FindAsync(code.UserId);
        if (user is null || user.IsBlocked)
        {
            return TokenError("invalid_grant");
        }

        var grant = new McpGrant
        {
            ClientName = code.ClientName,
            RedirectHost = new Uri(code.RedirectUri).Host,
            CurrentRefreshTokenId = McpTokenService.NewId(),
        };

        user.McpGrants ??= [];
        user.McpGrants.Add(grant);
        // Each fresh connect registers a new client, so reconnecting leaves the old grant behind.
        // Keep the most recently used ones rather than letting the list grow without end.
        if (user.McpGrants.Count > MaxGrantsPerUser)
        {
            user.McpGrants = user.McpGrants.OrderByDescending(g => g.LastUsedAt).Take(MaxGrantsPerUser).ToList();
        }

        await db.SaveChangesAsync();
        return Ok(IssueTokens(user.Id, grant, code.ClientId, code.Resource));
    }

    /// <summary>
    /// Rotates: the presented refresh token is spent and a new one issued. One that has already been
    /// spent means two parties hold copies of it — the user's Claude and someone else — and since
    /// there's no telling which is which, the whole connection is revoked.
    /// </summary>
    private async Task<IActionResult> RefreshAsync(IFormCollection form)
    {
        var token = tokens.ReadRefreshToken(form["refresh_token"].ToString());
        if (token is null)
        {
            return TokenError("invalid_grant");
        }

        var clientId = NullIfEmpty(form["client_id"]);
        if (clientId is not null && clientId != token.ClientId)
        {
            return TokenError("invalid_grant");
        }

        var user = await db.Users.FindAsync(token.UserId);
        var grant = user?.McpGrants?.FirstOrDefault(g => g.Id == token.GrantId);
        if (user is null || user.IsBlocked || grant is null)
        {
            return TokenError("invalid_grant");
        }

        if (grant.CurrentRefreshTokenId != token.TokenId)
        {
            logger.LogWarning("MCP refresh token reused for grant {GrantId}; revoking the connection.", grant.Id);
            user.McpGrants!.Remove(grant);
            await db.SaveChangesAsync();
            return TokenError("invalid_grant");
        }

        grant.CurrentRefreshTokenId = McpTokenService.NewId();
        grant.LastUsedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        return Ok(IssueTokens(user.Id, grant, token.ClientId, token.Resource));
    }

    private object IssueTokens(string userId, McpGrant grant, string clientId, string resource) => new
    {
        access_token = tokens.CreateAccessToken(new McpAccessToken(userId, grant.Id, resource)),
        token_type = "Bearer",
        expires_in = (int)McpTokenService.AccessTokenLifetime.TotalSeconds,
        refresh_token = tokens.CreateRefreshToken(
            new McpRefreshToken(userId, grant.Id, grant.CurrentRefreshTokenId, clientId, resource)),
        scope = McpOAuth.Scope,
    };

    private BadRequestObjectResult TokenError(string error) => BadRequest(new { error });

    // --- The user's own connections ------------------------------------------------------------------

    public record McpConnectionDto(string Id, string ClientName, string RedirectHost, DateTimeOffset CreatedAt, DateTimeOffset LastUsedAt);

    [HttpGet("api/mcp/connections")]
    [Authorize]
    public async Task<ActionResult<List<McpConnectionDto>>> GetConnections()
    {
        var user = await db.Users.FindAsync(User.CurrentUserId());
        return Ok((user?.McpGrants ?? [])
            .OrderByDescending(g => g.LastUsedAt)
            .Select(g => new McpConnectionDto(g.Id, g.ClientName, g.RedirectHost, g.CreatedAt, g.LastUsedAt))
            .ToList());
    }

    /// <summary>
    /// Takes effect on Claude's very next request: the bearer handler looks the grant up every time,
    /// so the still-unexpired access token stops working at once rather than within the hour.
    /// </summary>
    [HttpDelete("api/mcp/connections/{grantId}")]
    [Authorize]
    public async Task<IActionResult> Disconnect(string grantId)
    {
        var user = await db.Users.FindAsync(User.CurrentUserId());
        var grant = user?.McpGrants?.FirstOrDefault(g => g.Id == grantId);
        if (grant is null)
        {
            return NotFound();
        }

        user!.McpGrants!.Remove(grant);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static string? NullIfEmpty(Microsoft.Extensions.Primitives.StringValues value) =>
        string.IsNullOrEmpty(value.ToString()) ? null : value.ToString();

    private static string WithQuery(string uri, params (string Key, string? Value)[] parameters)
    {
        var query = string.Join("&", parameters
            .Where(p => p.Value is not null)
            .Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value!)}"));
        return uri + (uri.Contains('?') ? "&" : "?") + query;
    }
}
