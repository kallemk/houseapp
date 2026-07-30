using System.Net;
using System.Net.Http.Json;
using HouseApp.Api.Data;
using HouseApp.Api.Dtos.Auth;
using HouseApp.Api.Dtos.Documents;
using HouseApp.Api.Dtos.Projects;
using HouseApp.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HouseApp.Api.Tests;

public class DocumentsControllerTests : IClassFixture<HouseAppWebApplicationFactory>
{
    private readonly HouseAppWebApplicationFactory _factory;
    private static readonly PasswordHasher<ApplicationUser> Hasher = new();

    public DocumentsControllerTests(HouseAppWebApplicationFactory factory)
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
            var user = new ApplicationUser
            {
                Email = email,
                DisplayName = "Test User",
                PasswordHash = string.Empty,
                IsAdmin = true,
            };
            user.PasswordHash = Hasher.HashPassword(user, password);
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        return client;
    }

    private static Task<HttpResponseMessage> CreateDocumentAsync(
        HttpClient client,
        string propertyId,
        string? projectId = null,
        string fileName = "kvitto.pdf",
        string? title = "Ett kvitto") =>
        client.PostAsJsonAsync(
            "/api/documents",
            new CreateDocumentRequest(
                propertyId,
                projectId,
                new DateOnly(2026, 3, 1),
                title,
                fileName,
                "application/pdf",
                $"{propertyId}/{fileName}",
                1024,
                DocumentCategory.Receipt));

    /// <summary>
    /// The field was renamed RenovationEntryId → ProjectId in the project migration; the frontend
    /// kept posting the old name for a while, so every attachment silently became null. This pins
    /// the wire contract down.
    /// </summary>
    [Fact]
    public async Task Create_WithProjectId_KeepsTheLink()
    {
        var client = await CreateAuthenticatedClientAsync();
        var property = await TestData.CreatePropertyAsync(client);
        var project = await (await client.PostAsJsonAsync(
            $"/api/properties/{property.Id}/projects",
            TestData.SaveProject("Takbyte", "Roof"))).Content.ReadFromJsonAsync<ProjectDto>();

        var create = await CreateDocumentAsync(client, property.Id, project!.Id);
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);

        var listed = await (await client.GetAsync($"/api/properties/{property.Id}/documents"))
            .Content.ReadFromJsonAsync<List<DocumentDto>>();

        Assert.Equal(project.Id, listed!.Single(d => d.FileName == "kvitto.pdf").ProjectId);
    }

    [Fact]
    public async Task SetProject_AttachesAndDetaches()
    {
        var client = await CreateAuthenticatedClientAsync();
        var property = await TestData.CreatePropertyAsync(client);
        var project = await (await client.PostAsJsonAsync(
            $"/api/properties/{property.Id}/projects",
            TestData.SaveProject("Fasad", "Facade"))).Content.ReadFromJsonAsync<ProjectDto>();

        var document = await (await CreateDocumentAsync(client, property.Id, projectId: null, fileName: "offert.pdf"))
            .Content.ReadFromJsonAsync<DocumentDto>();
        Assert.Null(document!.ProjectId);

        var attach = await client.PutAsJsonAsync(
            $"/api/documents/{document.Id}/project?propertyId={property.Id}",
            new SetDocumentProjectRequest(project!.Id));
        Assert.Equal(HttpStatusCode.NoContent, attach.StatusCode);

        var afterAttach = await (await client.GetAsync($"/api/properties/{property.Id}/documents"))
            .Content.ReadFromJsonAsync<List<DocumentDto>>();
        Assert.Equal(project.Id, afterAttach!.Single(d => d.Id == document.Id).ProjectId);

        var detach = await client.PutAsJsonAsync(
            $"/api/documents/{document.Id}/project?propertyId={property.Id}",
            new SetDocumentProjectRequest(null));
        Assert.Equal(HttpStatusCode.NoContent, detach.StatusCode);

        var afterDetach = await (await client.GetAsync($"/api/properties/{property.Id}/documents"))
            .Content.ReadFromJsonAsync<List<DocumentDto>>();
        Assert.Null(afterDetach!.Single(d => d.Id == document.Id).ProjectId);
    }

    [Fact]
    public async Task SetProject_ForAnotherPropertysDocument_ReturnsNotFound()
    {
        var client = await CreateAuthenticatedClientAsync();
        var a = await TestData.CreatePropertyAsync(client, "House A");
        var b = await TestData.CreatePropertyAsync(client, "House B");

        var document = await (await CreateDocumentAsync(client, a.Id)).Content.ReadFromJsonAsync<DocumentDto>();

        var response = await client.PutAsJsonAsync(
            $"/api/documents/{document!.Id}/project?propertyId={b.Id}",
            new SetDocumentProjectRequest(null));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Create_WithoutATitle_IsRejected(string? title)
    {
        // A filename like "scan_0042.pdf" says nothing, which is the whole reason titles exist —
        // so uploading without one is refused rather than quietly falling back.
        var client = await CreateAuthenticatedClientAsync();
        var property = await TestData.CreatePropertyAsync(client);

        var response = await CreateDocumentAsync(client, property.Id, title: title);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_TrimsTheTitle()
    {
        var client = await CreateAuthenticatedClientAsync();
        var property = await TestData.CreatePropertyAsync(client);

        var created = await (await CreateDocumentAsync(client, property.Id, title: "  Besiktningsprotokoll  "))
            .Content.ReadFromJsonAsync<DocumentDto>();

        Assert.Equal("Besiktningsprotokoll", created!.Title);
    }

    [Fact]
    public async Task SetProject_WithoutAuth_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.PutAsJsonAsync(
            $"/api/documents/{Guid.NewGuid()}/project?propertyId={Guid.NewGuid()}",
            new SetDocumentProjectRequest(null));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
