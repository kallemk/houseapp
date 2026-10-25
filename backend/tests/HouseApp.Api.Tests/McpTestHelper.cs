using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HouseApp.Api.Data;
using HouseApp.Api.Dtos.Auth;
using HouseApp.Api.Mcp;
using HouseApp.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Xunit;

namespace HouseApp.Api.Tests;

/// <summary>
/// Walks the OAuth flow the way Claude does — register, authorize, consent as a signed-in user, swap
/// the code for tokens — and then talks MCP with the SDK's own client, so the tests exercise the real
/// protocol rather than hand-written JSON-RPC.
/// </summary>
public class McpTestHelper(HouseAppWebApplicationFactory factory)
{
    private static readonly PasswordHasher<ApplicationUser> Hasher = new();
    public const string Resource = "http://localhost/mcp";

    public record SignedIn(HttpClient Browser, ApplicationUser User);

    public record Tokens(string AccessToken, string RefreshToken, string ClientId);

    /// <summary>A user with a browser session — the cookie the consent page needs. Redirects aren't followed.</summary>
    public async Task<SignedIn> SignInAsync(string displayName = "Kalle")
    {
        var user = new ApplicationUser { Email = $"{Guid.NewGuid()}@example.com", DisplayName = displayName };
        user.PasswordHash = Hasher.HashPassword(user, "Secret123!");
        await SeedAsync(db => db.Users.Add(user));

        var browser = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var login = await browser.PostAsJsonAsync("/api/auth/login", new LoginRequest(user.Email, "Secret123!"));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        return new SignedIn(browser, user);
    }

    public async Task SeedAsync(Action<AppDbContext> seed)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        seed(db);
        await db.SaveChangesAsync();
    }

    public async Task<T> ReadAsync<T>(Func<AppDbContext, Task<T>> read)
    {
        using var scope = factory.Services.CreateScope();
        return await read(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    public async Task<string> RegisterAsync(HttpClient client, string redirectUri = McpOAuth.ClaudeCallback)
    {
        var response = await client.PostAsJsonAsync("/oauth/register", new Dictionary<string, object>
        {
            ["redirect_uris"] = new[] { redirectUri },
            ["client_name"] = "Claude",
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("client_id").GetString()!;
    }

    public static (string Verifier, string Challenge) NewPkce()
    {
        var verifier = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var challenge = WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        return (verifier, challenge);
    }

    public static string AuthorizeUrl(string clientId, string challenge, string redirectUri = McpOAuth.ClaudeCallback, string? resource = Resource) =>
        "/oauth/authorize?response_type=code" +
        $"&client_id={Uri.EscapeDataString(clientId)}" +
        $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
        $"&code_challenge={challenge}&code_challenge_method=S256&state=xyz" +
        (resource is null ? "" : $"&resource={Uri.EscapeDataString(resource)}");

    /// <summary>Authorize, then approve on the consent page. Returns the code Claude would receive.</summary>
    public async Task<string> AuthorizeAndApproveAsync(HttpClient browser, string clientId, string challenge, string redirectUri = McpOAuth.ClaudeCallback)
    {
        var authorize = await browser.GetAsync(AuthorizeUrl(clientId, challenge, redirectUri));
        Assert.Equal(HttpStatusCode.Redirect, authorize.StatusCode);
        var consentUrl = authorize.Headers.Location!.ToString();
        Assert.StartsWith("/oauth/consent?request=", consentUrl);
        var request = Uri.UnescapeDataString(consentUrl["/oauth/consent?request=".Length..]);

        var decision = await browser.PostAsJsonAsync("/api/oauth/consent", new { request, approve = true });
        Assert.Equal(HttpStatusCode.OK, decision.StatusCode);
        using var body = JsonDocument.Parse(await decision.Content.ReadAsStringAsync());
        var redirectUrl = new Uri(body.RootElement.GetProperty("redirectUrl").GetString()!);
        var query = QueryHelpers.ParseQuery(redirectUrl.Query);
        Assert.Equal("xyz", query["state"].ToString());
        return query["code"].ToString();
    }

    public static Task<HttpResponseMessage> PostTokenAsync(HttpClient client, Dictionary<string, string> form) =>
        client.PostAsync("/oauth/token", new FormUrlEncodedContent(form));

    public async Task<Tokens> ConnectAsync(SignedIn signedIn)
    {
        var clientId = await RegisterAsync(signedIn.Browser);
        var (verifier, challenge) = NewPkce();
        var code = await AuthorizeAndApproveAsync(signedIn.Browser, clientId, challenge);

        var response = await PostTokenAsync(signedIn.Browser, new()
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = McpOAuth.ClaudeCallback,
            ["client_id"] = clientId,
            ["code_verifier"] = verifier,
            ["resource"] = Resource,
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return new Tokens(
            body.RootElement.GetProperty("access_token").GetString()!,
            body.RootElement.GetProperty("refresh_token").GetString()!,
            clientId);
    }

    public async Task<McpClient> McpClientAsync(string accessToken)
    {
        var http = factory.CreateClient();
        var transport = new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = new Uri(http.BaseAddress!, "/mcp"),
                AdditionalHeaders = new Dictionary<string, string> { ["Authorization"] = $"Bearer {accessToken}" },
            },
            http);
        return await McpClient.CreateAsync(transport);
    }

    /// <summary>A tool's JSON result, or its error text when IsError.</summary>
    public static async Task<(bool IsError, JsonElement Json, string Text)> CallAsync(
        McpClient client, string tool, Dictionary<string, object?> arguments)
    {
        var result = await client.CallToolAsync(tool, arguments);
        var text = string.Concat(result.Content.OfType<TextContentBlock>().Select(t => t.Text));
        if (result.IsError == true)
        {
            return (true, default, text);
        }

        return (false, JsonDocument.Parse(text).RootElement.Clone(), text);
    }
}
