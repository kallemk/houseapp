using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using HouseApp.Api.Mcp;
using Xunit;

namespace HouseApp.Api.Tests;

/// <summary>
/// The OAuth flow in front of /mcp, held to what Claude requires (claude.com/docs/connectors/building/authentication):
/// a 401 that points at the metadata, DCR, S256 PKCE, single-use codes and rotating refresh tokens —
/// and to the app's own rules: cookie and bearer never cross over, and revocation is immediate.
/// </summary>
public class McpOAuthTests : IClassFixture<HouseAppWebApplicationFactory>
{
    private readonly HouseAppWebApplicationFactory _factory;
    private readonly McpTestHelper _mcp;

    public McpOAuthTests(HouseAppWebApplicationFactory factory)
    {
        _factory = factory;
        _mcp = new McpTestHelper(factory);
    }

    private static HttpRequestMessage McpPost(string? bearer)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = JsonContent.Create(new { jsonrpc = "2.0", id = 1, method = "tools/list" }),
        };
        if (bearer is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        }

        return request;
    }

    // --- Discovery ---------------------------------------------------------------------------------

    [Fact]
    public async Task AnUnauthenticatedCall_Is401_PointingAtTheResourceMetadata()
    {
        var response = await _factory.CreateClient().SendAsync(McpPost(null));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var header = response.Headers.WwwAuthenticate.ToString();
        Assert.Contains("resource_metadata=\"http://localhost/.well-known/oauth-protected-resource\"", header);
    }

    [Fact]
    public async Task TheMetadata_DescribesThisServerAsClaudeExpects()
    {
        var client = _factory.CreateClient();

        using var resource = JsonDocument.Parse(await client.GetStringAsync("/.well-known/oauth-protected-resource"));
        Assert.Equal(McpTestHelper.Resource, resource.RootElement.GetProperty("resource").GetString());
        Assert.Equal("http://localhost", resource.RootElement.GetProperty("authorization_servers")[0].GetString());

        using var server = JsonDocument.Parse(await client.GetStringAsync("/.well-known/oauth-authorization-server"));
        var root = server.RootElement;
        Assert.Equal("http://localhost/oauth/register", root.GetProperty("registration_endpoint").GetString());
        Assert.Equal(["S256"], root.GetProperty("code_challenge_methods_supported").EnumerateArray().Select(e => e.GetString()));
        Assert.Contains("none", root.GetProperty("token_endpoint_auth_methods_supported").EnumerateArray().Select(e => e.GetString()));
        Assert.Contains("offline_access", root.GetProperty("scopes_supported").EnumerateArray().Select(e => e.GetString()));
    }

    // --- Registration ------------------------------------------------------------------------------

    [Theory]
    [InlineData(McpOAuth.ClaudeCallback)]
    [InlineData("http://localhost:51234/callback")]
    [InlineData("http://127.0.0.1:6274/oauth/callback")]
    public async Task Registration_AcceptsClaudesRedirects(string redirectUri)
    {
        await _mcp.RegisterAsync(_factory.CreateClient(), redirectUri);
    }

    [Theory]
    [InlineData("https://evil.example.com/callback")]
    [InlineData("https://claude.ai.evil.example.com/api/mcp/auth_callback")]
    [InlineData("https://localhost/callback")]
    public async Task Registration_RefusesAnyOtherRedirect(string redirectUri)
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/oauth/register", new Dictionary<string, object>
        {
            ["redirect_uris"] = new[] { redirectUri },
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // --- Authorization -----------------------------------------------------------------------------

    [Fact]
    public async Task AFullConnection_ReachesTheTools()
    {
        var signedIn = await _mcp.SignInAsync();
        var tokens = await _mcp.ConnectAsync(signedIn);

        await using var client = await _mcp.McpClientAsync(tokens.AccessToken);
        var tools = await client.ListToolsAsync();

        Assert.Contains(tools, t => t.Name == "create_project");
        Assert.DoesNotContain(tools, t => t.Name.Contains("delete"));
    }

    [Fact]
    public async Task AnUnregisteredRedirect_AtAuthorize_IsRefusedWithoutRedirecting()
    {
        var signedIn = await _mcp.SignInAsync();
        var clientId = await _mcp.RegisterAsync(signedIn.Browser);
        var (_, challenge) = McpTestHelper.NewPkce();

        var response = await signedIn.Browser.GetAsync(
            McpTestHelper.AuthorizeUrl(clientId, challenge, redirectUri: "https://evil.example.com/callback"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    [Fact]
    public async Task ALoopbackRedirect_MatchesOnAnyPort()
    {
        // Claude Code picks a new port every session, so the registered one means nothing.
        var signedIn = await _mcp.SignInAsync();
        var clientId = await _mcp.RegisterAsync(signedIn.Browser, "http://localhost:1111/callback");
        var (_, challenge) = McpTestHelper.NewPkce();

        var code = await _mcp.AuthorizeAndApproveAsync(signedIn.Browser, clientId, challenge, "http://localhost:2222/callback");

        Assert.False(string.IsNullOrEmpty(code));
    }

    [Fact]
    public async Task AnotherResource_IsRefused()
    {
        var signedIn = await _mcp.SignInAsync();
        var clientId = await _mcp.RegisterAsync(signedIn.Browser);
        var (_, challenge) = McpTestHelper.NewPkce();

        var response = await signedIn.Browser.GetAsync(
            McpTestHelper.AuthorizeUrl(clientId, challenge, resource: "https://other.example.com/mcp"));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("error=invalid_target", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Consent_RequiresASignedInUser()
    {
        var signedIn = await _mcp.SignInAsync();
        var clientId = await _mcp.RegisterAsync(signedIn.Browser);
        var (_, challenge) = McpTestHelper.NewPkce();
        var authorize = await signedIn.Browser.GetAsync(McpTestHelper.AuthorizeUrl(clientId, challenge));
        var request = Uri.UnescapeDataString(authorize.Headers.Location!.ToString()["/oauth/consent?request=".Length..]);

        var anonymous = await _factory.CreateClient().PostAsJsonAsync("/api/oauth/consent", new { request, approve = true });

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
    }

    // --- Token -------------------------------------------------------------------------------------

    private async Task<(McpTestHelper.SignedIn SignedIn, string ClientId, string Code, string Verifier)> ApprovedCodeAsync()
    {
        var signedIn = await _mcp.SignInAsync();
        var clientId = await _mcp.RegisterAsync(signedIn.Browser);
        var (verifier, challenge) = McpTestHelper.NewPkce();
        var code = await _mcp.AuthorizeAndApproveAsync(signedIn.Browser, clientId, challenge);
        return (signedIn, clientId, code, verifier);
    }

    private static Dictionary<string, string> CodeExchange(string clientId, string code, string verifier) => new()
    {
        ["grant_type"] = "authorization_code",
        ["code"] = code,
        ["redirect_uri"] = McpOAuth.ClaudeCallback,
        ["client_id"] = clientId,
        ["code_verifier"] = verifier,
    };

    private static async Task AssertInvalidGrantAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("invalid_grant", body.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task TheWrongPkceVerifier_IsInvalidGrant()
    {
        var (signedIn, clientId, code, _) = await ApprovedCodeAsync();

        var response = await McpTestHelper.PostTokenAsync(signedIn.Browser, CodeExchange(clientId, code, "wrong-verifier"));

        await AssertInvalidGrantAsync(response);
    }

    [Fact]
    public async Task ACode_CanOnlyBeUsedOnce()
    {
        var (signedIn, clientId, code, verifier) = await ApprovedCodeAsync();

        var first = await McpTestHelper.PostTokenAsync(signedIn.Browser, CodeExchange(clientId, code, verifier));
        var second = await McpTestHelper.PostTokenAsync(signedIn.Browser, CodeExchange(clientId, code, verifier));

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        await AssertInvalidGrantAsync(second);
    }

    [Fact]
    public async Task Refresh_Rotates_AndReusingAnOldToken_RevokesTheConnection()
    {
        var signedIn = await _mcp.SignInAsync();
        var tokens = await _mcp.ConnectAsync(signedIn);

        var refreshed = await McpTestHelper.PostTokenAsync(signedIn.Browser, new()
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = tokens.RefreshToken,
            ["client_id"] = tokens.ClientId,
        });
        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        using var body = JsonDocument.Parse(await refreshed.Content.ReadAsStringAsync());
        var newAccess = body.RootElement.GetProperty("access_token").GetString()!;
        Assert.NotEqual(tokens.RefreshToken, body.RootElement.GetProperty("refresh_token").GetString());

        // The spent token again: someone else has a copy, so the whole connection goes.
        var replay = await McpTestHelper.PostTokenAsync(signedIn.Browser, new()
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = tokens.RefreshToken,
        });
        await AssertInvalidGrantAsync(replay);

        var call = await _factory.CreateClient().SendAsync(McpPost(newAccess));
        Assert.Equal(HttpStatusCode.Unauthorized, call.StatusCode);
    }

    // --- Revocation and separation ------------------------------------------------------------------

    [Fact]
    public async Task Disconnecting_TakesEffectOnTheNextCall()
    {
        var signedIn = await _mcp.SignInAsync();
        var tokens = await _mcp.ConnectAsync(signedIn);

        var connections = await signedIn.Browser.GetFromJsonAsync<List<JsonElement>>("/api/mcp/connections");
        var grantId = Assert.Single(connections!).GetProperty("id").GetString();
        var disconnect = await signedIn.Browser.DeleteAsync($"/api/mcp/connections/{grantId}");
        Assert.Equal(HttpStatusCode.NoContent, disconnect.StatusCode);

        var call = await _factory.CreateClient().SendAsync(McpPost(tokens.AccessToken));
        Assert.Equal(HttpStatusCode.Unauthorized, call.StatusCode);
    }

    [Fact]
    public async Task ABlockedUser_LosesMcpAccessAtOnce()
    {
        var signedIn = await _mcp.SignInAsync();
        var tokens = await _mcp.ConnectAsync(signedIn);

        await _mcp.SeedAsync(db => db.Users.Find(signedIn.User.Id)!.IsBlocked = true);

        var call = await _factory.CreateClient().SendAsync(McpPost(tokens.AccessToken));
        Assert.Equal(HttpStatusCode.Unauthorized, call.StatusCode);
    }

    [Fact]
    public async Task TheSessionCookie_IsNotAcceptedOnMcp()
    {
        var signedIn = await _mcp.SignInAsync();

        var call = await signedIn.Browser.SendAsync(McpPost(null));

        Assert.Equal(HttpStatusCode.Unauthorized, call.StatusCode);
    }

    [Fact]
    public async Task ClaudesToken_IsNotAcceptedOnTheApi()
    {
        var signedIn = await _mcp.SignInAsync();
        var tokens = await _mcp.ConnectAsync(signedIn);

        var api = _factory.CreateClient();
        api.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        Assert.Equal(HttpStatusCode.Unauthorized, (await api.GetAsync("/api/properties")).StatusCode);
    }

    [Fact]
    public async Task ATamperedToken_Is401()
    {
        var call = await _factory.CreateClient().SendAsync(McpPost("not-a-real-token"));

        Assert.Equal(HttpStatusCode.Unauthorized, call.StatusCode);
    }
}
