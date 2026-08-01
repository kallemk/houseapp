using System.Net;
using System.Net.Http.Json;
using HouseApp.Api.Data;
using HouseApp.Api.Dtos.Auth;
using HouseApp.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HouseApp.Api.Tests;

public class AuthControllerTests : IClassFixture<HouseAppWebApplicationFactory>
{
    private readonly HouseAppWebApplicationFactory _factory;
    private static readonly PasswordHasher<ApplicationUser> Hasher = new();

    public AuthControllerTests(HouseAppWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task SeedUserAsync(string email, string password, string displayName = "Test User")
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = new ApplicationUser { Email = email, DisplayName = displayName, PasswordHash = string.Empty };
        user.PasswordHash = Hasher.HashPassword(user, password);
        db.Users.Add(user);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Login_WithCorrectPassword_ReturnsOkAndAllowsMe()
    {
        await SeedUserAsync("login-ok@example.com", "Secret123!");
        var client = _factory.CreateClient();

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("login-ok@example.com", "Secret123!"));
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var meResponse = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);

        var me = await meResponse.Content.ReadFromJsonAsync<MeResponse>();
        Assert.Equal("login-ok@example.com", me!.Email);
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorized()
    {
        await SeedUserAsync("login-bad@example.com", "Secret123!");
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("login-bad@example.com", "WrongPassword"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_WithoutLogin_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// The critical guarantee of the whole Google migration: signing in with Google must resolve to
    /// the *existing* ApplicationUser row, keeping its Id — that id is what's stored in
    /// Property.MemberUserIds and every *CreatedByUserId.
    /// </summary>
    [Fact]
    public async Task GoogleLogin_WithAllowlistedEmail_SignsInAsTheExistingUser()
    {
        const string email = "google-ok@example.com";
        await SeedUserAsync(email, "Secret123!", "Existing Person");

        string existingId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            existingId = db.Users.Single(u => u.Email == email).Id;
        }

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/google", new GoogleLoginRequest(email));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<MeResponse>();
        Assert.Equal(existingId, body!.Id);
        Assert.Equal("Existing Person", body.DisplayName);

        // The issued cookie must work on a normal authorized endpoint.
        var meResponse = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
        Assert.Equal(existingId, (await meResponse.Content.ReadFromJsonAsync<MeResponse>())!.Id);
    }

    [Fact]
    public async Task GoogleLogin_MatchesEmailCaseInsensitively()
    {
        await SeedUserAsync("Google-Case@Example.com", "Secret123!");
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/google", new GoogleLoginRequest("google-case@example.com"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GoogleLogin_WithAnUnknownEmail_CreatesTheAccount()
    {
        // The app is published on Google, so this is open registration: the users container stopped
        // being an allowlist. It used to answer 403 here.
        var email = $"newcomer-{Guid.NewGuid()}@example.com";
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/google", new GoogleLoginRequest(email));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var me = await response.Content.ReadFromJsonAsync<MeResponse>();
        Assert.Equal(email, me!.Email);
        // Never an admin by default — that stays something an existing admin grants deliberately.
        Assert.False(me.IsAdmin);
    }

    [Fact]
    public async Task GoogleLogin_Twice_ReusesTheSameAccount()
    {
        // The id is stored in Property.MemberUserIds and every *CreatedByUserId with no way to
        // rewrite it, so minting a second account for the same person on their next sign-in would
        // silently cut them off from everything they belong to.
        var email = $"returning-{Guid.NewGuid()}@example.com";

        var first = await _factory.CreateClient().PostAsJsonAsync("/api/auth/google", new GoogleLoginRequest(email));
        var second = await _factory.CreateClient().PostAsJsonAsync("/api/auth/google", new GoogleLoginRequest(email));

        var firstMe = await first.Content.ReadFromJsonAsync<MeResponse>();
        var secondMe = await second.Content.ReadFromJsonAsync<MeResponse>();
        Assert.Equal(firstMe!.Id, secondMe!.Id);
    }

    [Fact]
    public async Task GoogleLogin_ForAnExistingPasswordAccount_KeepsItsId()
    {
        // The same guarantee from the other direction: someone added by email who later signs in
        // with Google must land on their existing account, not a fresh one.
        var email = $"existing-{Guid.NewGuid()}@example.com";
        await SeedUserAsync(email, "Secret123!");

        var password = await _factory.CreateClient()
            .PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "Secret123!"));
        var viaPassword = await password.Content.ReadFromJsonAsync<MeResponse>();

        var google = await _factory.CreateClient().PostAsJsonAsync("/api/auth/google", new GoogleLoginRequest(email));
        var viaGoogle = await google.Content.ReadFromJsonAsync<MeResponse>();

        Assert.Equal(viaPassword!.Id, viaGoogle!.Id);
    }

    [Fact]
    public async Task ABlockedAccount_IsRefusedOnBothSignInPaths()
    {
        var email = $"blocked-{Guid.NewGuid()}@example.com";
        await SeedUserAsync(email, "Secret123!");
        await SetBlockedAsync(email, true);

        var google = await _factory.CreateClient()
            .PostAsJsonAsync("/api/auth/google", new GoogleLoginRequest(email));
        var password = await _factory.CreateClient()
            .PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "Secret123!"));

        Assert.Equal(HttpStatusCode.Forbidden, google.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, password.StatusCode);
    }

    [Fact]
    public async Task BlockingSomeoneEndsTheSessionTheyAlreadyHold()
    {
        // The cookie is a 14-day sliding session. Without a per-request check, "blocked" would mean
        // "blocked in up to two weeks", which is no use at all for removing someone.
        var email = $"blocked-live-{Guid.NewGuid()}@example.com";
        await SeedUserAsync(email, "Secret123!");

        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "Secret123!"));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me")).StatusCode);

        await SetBlockedAsync(email, true);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    private async Task SetBlockedAsync(string email, bool blocked)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = (await db.Users.ToListAsync())
            .Single(u => string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase));
        user.IsBlocked = blocked;
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task GoogleLogin_WithUnverifiedEmail_ReturnsUnauthorized()
    {
        await SeedUserAsync("unverified-user@example.com", "Secret123!");
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/google", new GoogleLoginRequest("unverified:unverified-user@example.com"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GoogleLogin_WithInvalidToken_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/google", new GoogleLoginRequest("invalid"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_ForGoogleOnlyAccountWithNoPassword_ReturnsUnauthorized()
    {
        // Users added via the admin page without a password must not be loggable-in via the
        // password endpoint — guards against an empty/null hash being treated as a match.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Users.Add(new ApplicationUser { Email = "nopassword@example.com", DisplayName = "Google Only" });
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("nopassword@example.com", ""));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// The auth cookie must be *persistent* — carrying an Expires/Max-Age — not a session cookie.
    ///
    /// This is asserted on the raw header because the failure mode is an **absent** attribute, which
    /// nothing else would notice: without AuthenticationProperties.IsPersistent the app still signs
    /// people in perfectly, and ExpireTimeSpan/SlidingExpiration still read as a 14-day sliding
    /// session in config. The cookie just quietly evaporates whenever the browser session ends,
    /// which on Android Chrome is constantly.
    /// </summary>
    private static void AssertCookieOutlivesTheBrowserSession(HttpResponseMessage response)
    {
        var setCookie = Assert.Single(
            response.Headers.GetValues("Set-Cookie"),
            c => c.StartsWith("houseapp.auth=", StringComparison.Ordinal));

        Assert.True(
            setCookie.Contains("expires=", StringComparison.OrdinalIgnoreCase)
            || setCookie.Contains("max-age=", StringComparison.OrdinalIgnoreCase),
            $"The auth cookie has no expiry, so it is a session cookie the browser may drop at any time: {setCookie}");
    }

    [Fact]
    public async Task Login_IssuesAPersistentCookie()
    {
        await SeedUserAsync("persistent-password@example.com", "Secret123!");
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest("persistent-password@example.com", "Secret123!"));

        AssertCookieOutlivesTheBrowserSession(response);
    }

    [Fact]
    public async Task GoogleLogin_IssuesAPersistentCookie()
    {
        // Both sign-in paths funnel through the same private SignInAsync, but that's exactly the
        // kind of thing a refactor separates — so both are pinned.
        const string email = "persistent-google@example.com";
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Users.Add(new ApplicationUser { Email = email, DisplayName = "Google Only" });
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/google", new GoogleLoginRequest(email));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertCookieOutlivesTheBrowserSession(response);
    }
}
