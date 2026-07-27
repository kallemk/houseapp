using System.Net;
using System.Net.Http.Json;
using HouseApp.Api.Data;
using HouseApp.Api.Dtos.Auth;
using HouseApp.Api.Dtos.Properties;
using HouseApp.Api.Dtos.Users;
using HouseApp.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HouseApp.Api.Tests;

public class UsersControllerTests : IClassFixture<HouseAppWebApplicationFactory>
{
    private readonly HouseAppWebApplicationFactory _factory;
    private static readonly PasswordHasher<ApplicationUser> Hasher = new();

    public UsersControllerTests(HouseAppWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<(HttpClient Client, string UserId)> CreateAuthenticatedClientAsync()
    {
        var email = $"{Guid.NewGuid()}@example.com";
        const string password = "Secret123!";
        string userId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = new ApplicationUser { Email = email, DisplayName = "Test User" };
            user.PasswordHash = Hasher.HashPassword(user, password);
            db.Users.Add(user);
            await db.SaveChangesAsync();
            userId = user.Id;
        }

        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        return (client, userId);
    }

    [Fact]
    public async Task Create_AddsUserThatCanThenSignInWithGoogle()
    {
        var (client, _) = await CreateAuthenticatedClientAsync();
        var newEmail = $"invited-{Guid.NewGuid()}@example.com";

        var create = await client.PostAsJsonAsync("/api/users", new CreateUserRequest(newEmail, "Invited Person", null));
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);

        var created = await create.Content.ReadFromJsonAsync<UserDto>();
        Assert.False(created!.HasPassword);

        // Being on the list is precisely what makes Google sign-in work.
        var googleClient = _factory.CreateClient();
        var googleLogin = await googleClient.PostAsJsonAsync("/api/auth/google", new GoogleLoginRequest(newEmail));
        Assert.Equal(HttpStatusCode.OK, googleLogin.StatusCode);
        Assert.Equal(created.Id, (await googleLogin.Content.ReadFromJsonAsync<MeResponse>())!.Id);
    }

    [Fact]
    public async Task Create_WithInitialPassword_AllowsPasswordLogin()
    {
        var (client, _) = await CreateAuthenticatedClientAsync();
        var newEmail = $"withpw-{Guid.NewGuid()}@example.com";

        var create = await client.PostAsJsonAsync("/api/users", new CreateUserRequest(newEmail, "Has Password", "InitPw123!"));
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        Assert.True((await create.Content.ReadFromJsonAsync<UserDto>())!.HasPassword);

        var loginClient = _factory.CreateClient();
        var login = await loginClient.PostAsJsonAsync("/api/auth/login", new LoginRequest(newEmail, "InitPw123!"));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    /// <summary>
    /// PropertiesController.Create only stamps users that exist at that moment into MemberUserIds,
    /// so without a backfill a newly invited person would sign in to an empty property list.
    /// </summary>
    [Fact]
    public async Task Create_BackfillsNewUserOntoExistingProperties()
    {
        var (client, _) = await CreateAuthenticatedClientAsync();

        var createProperty = await client.PostAsJsonAsync(
            "/api/properties",
            new CreatePropertyRequest("Pre-existing House", "1 Old St", new DateOnly(2020, 1, 1), 100000m));
        var property = await createProperty.Content.ReadFromJsonAsync<PropertyDto>();

        var newEmail = $"backfill-{Guid.NewGuid()}@example.com";
        await client.PostAsJsonAsync("/api/users", new CreateUserRequest(newEmail, "Late Joiner", null));

        // Sign in as the newly invited user and confirm they can see the pre-existing property.
        var newClient = _factory.CreateClient();
        await newClient.PostAsJsonAsync("/api/auth/google", new GoogleLoginRequest(newEmail));

        var list = await (await newClient.GetAsync("/api/properties")).Content.ReadFromJsonAsync<List<PropertyDto>>();
        Assert.Contains(list!, p => p.Id == property!.Id);
    }

    [Fact]
    public async Task Create_WithDuplicateEmail_ReturnsConflict()
    {
        var (client, _) = await CreateAuthenticatedClientAsync();
        var email = $"dupe-{Guid.NewGuid()}@example.com";

        await client.PostAsJsonAsync("/api/users", new CreateUserRequest(email, "First", null));
        var second = await client.PostAsJsonAsync("/api/users", new CreateUserRequest(email.ToUpperInvariant(), "Second", null));

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Delete_OwnAccount_IsBlocked()
    {
        var (client, userId) = await CreateAuthenticatedClientAsync();

        var response = await client.DeleteAsync($"/api/users/{userId}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Delete_OtherAccount_RemovesThemFromTheAllowlist()
    {
        var (client, _) = await CreateAuthenticatedClientAsync();
        var newEmail = $"removeme-{Guid.NewGuid()}@example.com";
        var create = await client.PostAsJsonAsync("/api/users", new CreateUserRequest(newEmail, "Temp Person", null));
        var created = await create.Content.ReadFromJsonAsync<UserDto>();

        var delete = await client.DeleteAsync($"/api/users/{created!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        // Removal from the users container must actually revoke Google sign-in.
        var googleClient = _factory.CreateClient();
        var googleLogin = await googleClient.PostAsJsonAsync("/api/auth/google", new GoogleLoginRequest(newEmail));
        Assert.Equal(HttpStatusCode.Forbidden, googleLogin.StatusCode);
    }

    [Fact]
    public async Task GetAll_WithoutAuth_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/users");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
