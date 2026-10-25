namespace HouseApp.Api.Models;

/// <summary>
/// One Claude client a user has connected over MCP — nested in the user document
/// (<see cref="ApplicationUser.McpGrants"/>), never a container of its own: it's only ever read
/// alongside the user, which every MCP request point-reads anyway.
///
/// Everything else in the OAuth flow is self-contained and protected with Data Protection; this is
/// the one piece of state, because two things can't be stateless: revoking a connection, and
/// noticing a refresh token being used twice.
/// </summary>
public class McpGrant
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>What the client called itself at registration, e.g. "Claude".</summary>
    public required string ClientName { get; set; }

    /// <summary>Where it was sent back to — claude.ai, or localhost for Claude Code.</summary>
    public required string RedirectHost { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Updated on each token refresh, not each request — at most hourly, so cheap.</summary>
    public DateTimeOffset LastUsedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// The only refresh token that may be redeemed next. Each refresh issues a new one and moves this
    /// on; presenting an older one means a token was copied, and the whole grant is revoked.
    /// </summary>
    public required string CurrentRefreshTokenId { get; set; }
}
