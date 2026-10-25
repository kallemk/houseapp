namespace HouseApp.Api.Mcp;

/// <summary>
/// The fixed parts of HusTracker's OAuth authorization server for Claude: where things live and
/// which clients may get tokens at all.
/// </summary>
public static class McpOAuth
{
    public const string McpPath = "/mcp";
    public const string Scope = "houses";
    public const string ProtectedResourceMetadataPath = "/.well-known/oauth-protected-resource";
    public const string AuthorizationServerMetadataPath = "/.well-known/oauth-authorization-server";

    /// <summary>The hosted Claude apps — web, Desktop, mobile — all come back here.</summary>
    public const string ClaudeCallback = "https://claude.ai/api/mcp/auth_callback";

    /// <summary>
    /// The origin as the caller sees it. Correct behind App Service because forwarded headers are
    /// applied first (see AddHouseAppRateLimiting), and the custom domain is the Host. Every URL in
    /// the metadata is built from this rather than configured, so production, local dev and the test
    /// server each describe themselves.
    /// </summary>
    public static string Origin(HttpRequest request) => $"{request.Scheme}://{request.Host}{request.PathBase}";

    public static string Resource(HttpRequest request) => Origin(request) + McpPath;

    /// <summary>
    /// Only Claude's own redirect targets are accepted at registration: the hosted callback, and a
    /// loopback address for Claude Code (and local tools like the MCP Inspector). Registration is
    /// open — anyone can call it — so this is what keeps tokens from being issued to some other
    /// website. A loopback redirect stays on the user's own machine, which is why its port and path
    /// are allowed to vary.
    /// </summary>
    public static bool IsAllowedRedirectUri(string redirectUri) =>
        redirectUri == ClaudeCallback || IsLoopback(redirectUri);

    public static bool IsLoopback(string redirectUri) =>
        Uri.TryCreate(redirectUri, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttp
        && uri.Host is "localhost" or "127.0.0.1"
        && string.IsNullOrEmpty(uri.UserInfo)
        && string.IsNullOrEmpty(uri.Fragment);

    /// <summary>
    /// Whether the redirect URI sent at authorize is one the client registered. Exact, except that a
    /// loopback URI matches whatever port it's on today — Claude Code picks a fresh port per session
    /// (RFC 8252 §7.3), so the registered port is meaningless.
    /// </summary>
    public static bool MatchesRegistered(string redirectUri, IEnumerable<string> registered) =>
        registered.Any(r => r == redirectUri || (IsLoopback(r) && IsLoopback(redirectUri) && SameIgnoringPort(r, redirectUri)));

    private static bool SameIgnoringPort(string a, string b)
    {
        var x = new Uri(a);
        var y = new Uri(b);
        return x.Host == y.Host && x.AbsolutePath == y.AbsolutePath && x.Query == y.Query;
    }

    /// <summary>
    /// RFC 8707: the token is for this server's /mcp and nothing else. Clients that send no
    /// resource get this one; a trailing slash is forgiven.
    /// </summary>
    public static bool IsThisResource(string? resource, HttpRequest request) =>
        resource is null || resource.TrimEnd('/') == Resource(request);
}
