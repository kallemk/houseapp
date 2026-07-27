using System.Net;
using System.Net.Http.Json;
using HouseApp.Api.Data;
using HouseApp.Api.Dtos.Auth;
using HouseApp.Api.Models;
using Microsoft.AspNetCore.Identity;
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
    public async Task GoogleLogin_WithEmailNotOnAllowlist_ReturnsForbidden()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/google", new GoogleLoginRequest("stranger@example.com"));

        // 403, not 401 — a real Google account that simply hasn't been invited. Also proves the
        // endpoint never auto-creates users.
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
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
}
