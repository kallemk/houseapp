using System.Net;
using System.Net.Http.Json;
using HouseApp.Api.Data;
using HouseApp.Api.Dtos.Auth;
using HouseApp.Api.Dtos.Properties;
using HouseApp.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HouseApp.Api.Tests;

public class PropertiesControllerTests : IClassFixture<HouseAppWebApplicationFactory>
{
    private readonly HouseAppWebApplicationFactory _factory;
    private static readonly PasswordHasher<ApplicationUser> Hasher = new();

    public PropertiesControllerTests(HouseAppWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        var email = $"{Guid.NewGuid()}@example.com";
        const string password = "Secret123!";

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = new ApplicationUser { Email = email, DisplayName = "Test User", PasswordHash = string.Empty };
            user.PasswordHash = Hasher.HashPassword(user, password);
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        return client;
    }

    [Fact]
    public async Task CreateThenGet_RoundTripsProperty()
    {
        var client = await CreateAuthenticatedClientAsync();

        var create = await client.PostAsJsonAsync(
            "/api/properties",
            new CreatePropertyRequest("Our House", "123 Main St", new DateOnly(2020, 6, 1), 350000m));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        var created = await create.Content.ReadFromJsonAsync<PropertyDto>();
        Assert.NotNull(created);

        var get = await client.GetAsync($"/api/properties/{created!.Id}");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);

        var fetched = await get.Content.ReadFromJsonAsync<PropertyDto>();
        Assert.Equal("Our House", fetched!.Nickname);
    }

    [Fact]
    public async Task GetAll_WithoutAuth_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/properties");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
