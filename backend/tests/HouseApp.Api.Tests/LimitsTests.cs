using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HouseApp.Api.Data;
using HouseApp.Api.Dtos.Auth;
using HouseApp.Api.Dtos.Budgets;
using HouseApp.Api.Dtos.Documents;
using HouseApp.Api.Dtos.Projects;
using HouseApp.Api.Dtos.Properties;
using HouseApp.Api.Dtos.PropertyComponents;
using HouseApp.Api.Dtos.Valuations;
using HouseApp.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HouseApp.Api.Tests;

/// <summary>
/// Every count limit in <see cref="Limits"/>: filled to the limit directly in the database, then
/// one more through the API. Seeding through the API would be hundreds of requests per test.
/// </summary>
public class LimitsTests : IClassFixture<HouseAppWebApplicationFactory>
{
    private readonly HouseAppWebApplicationFactory _factory;
    private static readonly PasswordHasher<ApplicationUser> Hasher = new();

    public LimitsTests(HouseAppWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<ApplicationUser> CreateUserAsync(string displayName = "Test User")
    {
        var user = new ApplicationUser { Email = $"{Guid.NewGuid()}@example.com", DisplayName = displayName };
        user.PasswordHash = Hasher.HashPassword(user, "Secret123!");
        await SeedAsync(db => db.Users.Add(user));
        return user;
    }

    private async Task<(HttpClient Client, ApplicationUser User)> CreateAuthenticatedClientAsync()
    {
        var user = await CreateUserAsync();
        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(user.Email, "Secret123!"));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        return (client, user);
    }

    private async Task SeedAsync(Action<AppDbContext> seed)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        seed(db);
        await db.SaveChangesAsync();
    }

    private static Property NewProperty(params string[] memberIds) => new()
    {
        Nickname = "Hus",
        Address = "Gatan 1",
        MemberUserIds = [.. memberIds],
    };

    private async Task<(HttpClient Client, ApplicationUser User, string PropertyId)> CreatePropertyAsync()
    {
        var (client, user) = await CreateAuthenticatedClientAsync();
        var property = await TestData.CreatePropertyAsync(client);
        return (client, user, property.Id);
    }

    private static async Task AssertLimitReachedAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(Limits.LimitReachedCode, body.RootElement.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(body.RootElement.GetProperty("message").GetString()));
    }

    // --- Properties and members ------------------------------------------------------------------

    [Fact]
    public async Task CreatingAPropertyBeyondTheLimit_IsRefused()
    {
        var (client, user) = await CreateAuthenticatedClientAsync();
        await SeedAsync(db =>
        {
            for (var i = 0; i < Limits.PropertiesPerUser; i++)
            {
                db.Properties.Add(NewProperty(user.Id));
            }
        });

        await AssertLimitReachedAsync(await client.PostAsJsonAsync("/api/properties", TestData.SaveProperty()));
    }

    [Fact]
    public async Task TheDemo_DoesNotCountTowardThePropertyLimit()
    {
        var (client, user) = await CreateAuthenticatedClientAsync();
        await SeedAsync(db =>
        {
            for (var i = 0; i < Limits.PropertiesPerUser - 1; i++)
            {
                db.Properties.Add(NewProperty(user.Id));
            }

            var demo = NewProperty(user.Id);
            demo.IsDemo = true;
            db.Properties.Add(demo);
        });

        var response = await client.PostAsJsonAsync("/api/properties", TestData.SaveProperty());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task AddingAMemberWhoIsAlreadyAtTheLimit_IsRefused()
    {
        var (client, _, propertyId) = await CreatePropertyAsync();
        var busy = await CreateUserAsync("Upptagen");
        await SeedAsync(db =>
        {
            for (var i = 0; i < Limits.PropertiesPerUser; i++)
            {
                db.Properties.Add(NewProperty(busy.Id));
            }
        });

        var response = await client.PostAsJsonAsync(
            $"/api/properties/{propertyId}/members", new AddPropertyMemberRequest(busy.Id));

        await AssertLimitReachedAsync(response);
    }

    [Fact]
    public async Task AddingMembersBeyondTheLimit_IsRefused()
    {
        var (client, user, _) = await CreatePropertyAsync();
        var property = NewProperty(user.Id);
        var others = new List<ApplicationUser>();
        for (var i = 1; i < Limits.MembersPerProperty; i++)
        {
            others.Add(await CreateUserAsync());
        }
        property.MemberUserIds!.AddRange(others.Select(o => o.Id));
        await SeedAsync(db => db.Properties.Add(property));
        var oneTooMany = await CreateUserAsync();

        var response = await client.PostAsJsonAsync(
            $"/api/properties/{property.Id}/members", new AddPropertyMemberRequest(oneTooMany.Id));

        await AssertLimitReachedAsync(response);
    }

    // --- Projects ----------------------------------------------------------------------------------

    [Fact]
    public async Task CreatingAProjectBeyondTheLimit_IsRefused_ButOneBelowIsFine()
    {
        var (client, user, propertyId) = await CreatePropertyAsync();
        await SeedAsync(db =>
        {
            for (var i = 0; i < Limits.ProjectsPerProperty - 1; i++)
            {
                db.Projects.Add(new Project { PropertyId = propertyId, Name = $"P{i}", ComponentId = "Roof", CreatedByUserId = user.Id });
            }
        });

        var last = await client.PostAsJsonAsync($"/api/properties/{propertyId}/projects", TestData.SaveProject("Sista", "Roof"));
        Assert.Equal(HttpStatusCode.OK, last.StatusCode);

        var oneTooMany = await client.PostAsJsonAsync($"/api/properties/{propertyId}/projects", TestData.SaveProject("En för mycket", "Roof"));
        await AssertLimitReachedAsync(oneTooMany);
    }

    [Fact]
    public async Task UpdatingAProjectWithTooManyCostRows_IsRefused()
    {
        var (client, _, propertyId) = await CreatePropertyAsync();
        var created = await client.PostAsJsonAsync($"/api/properties/{propertyId}/projects", TestData.SaveProject("Tak", "Roof"));
        var project = await created.Content.ReadFromJsonAsync<ProjectDto>();

        var costs = Enumerable.Range(0, Limits.CostsPerProject + 1)
            .Select(_ => new ProjectCostRequest(CostType.Materials, null, 1m, new DateOnly(2026, 1, 1), false))
            .ToList();
        var response = await client.PutAsJsonAsync(
            $"/api/projects/{project!.Id}?propertyId={propertyId}", TestData.SaveProject("Tak", "Roof", costs: costs));

        await AssertLimitReachedAsync(response);
    }

    [Fact]
    public async Task ATooLongProjectName_IsABadRequest()
    {
        var (client, _, propertyId) = await CreatePropertyAsync();

        var response = await client.PostAsJsonAsync(
            $"/api/properties/{propertyId}/projects",
            TestData.SaveProject(new string('x', Limits.ShortText + 1), "Roof"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // --- Budgets, valuations, components ------------------------------------------------------------

    [Fact]
    public async Task ANewBudgetYearBeyondTheLimit_IsRefused_ButAnExistingYearCanStillBeEdited()
    {
        var (client, _, propertyId) = await CreatePropertyAsync();
        await SeedAsync(db =>
        {
            for (var i = 0; i < Limits.BudgetYearsPerProperty; i++)
            {
                db.Budgets.Add(new Budget { PropertyId = propertyId, Year = 2000 + i });
            }
        });

        var newYear = await client.PutAsJsonAsync(
            $"/api/properties/{propertyId}/budgets/2090", new SaveBudgetRequest(2090, 1m, 1m, 1m, 1m));
        await AssertLimitReachedAsync(newYear);

        var existingYear = await client.PutAsJsonAsync(
            $"/api/properties/{propertyId}/budgets/2000", new SaveBudgetRequest(2000, 5m, 5m, 5m, 5m));
        Assert.Equal(HttpStatusCode.OK, existingYear.StatusCode);
    }

    [Fact]
    public async Task ABudgetYearOutsideTheRange_IsABadRequest()
    {
        var (client, _, propertyId) = await CreatePropertyAsync();

        var response = await client.PutAsJsonAsync(
            $"/api/properties/{propertyId}/budgets/1899", new SaveBudgetRequest(1899, 1m, 1m, 1m, 1m));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreatingAValuationBeyondTheLimit_IsRefused()
    {
        var (client, user, propertyId) = await CreatePropertyAsync();
        await SeedAsync(db =>
        {
            for (var i = 0; i < Limits.ValuationsPerProperty; i++)
            {
                db.ValuationEntries.Add(new ValuationEntry { PropertyId = propertyId, Value = 1m, CreatedByUserId = user.Id });
            }
        });

        var response = await client.PostAsJsonAsync(
            $"/api/properties/{propertyId}/valuations", new CreateValuationEntryRequest(new DateOnly(2026, 1, 1), 1m, null, null));

        await AssertLimitReachedAsync(response);
    }

    [Fact]
    public async Task CreatingALocalComponentBeyondTheLimit_IsRefused()
    {
        var (client, user, _) = await CreatePropertyAsync();
        var property = NewProperty(user.Id);
        property.ComponentsCustomized = true;
        property.LocalComponents = Enumerable.Range(0, Limits.LocalComponentsPerProperty)
            .Select(i => new PropertyLocalComponent { Name = $"K{i}" })
            .ToList();
        await SeedAsync(db => db.Properties.Add(property));

        var response = await client.PostAsJsonAsync(
            $"/api/properties/{property.Id}/components", new SavePropertyLocalComponentRequest("En till", 12));

        await AssertLimitReachedAsync(response);
    }

    // --- Documents ----------------------------------------------------------------------------------

    private static CreateDocumentRequest CreateDocument(string propertyId, string blobPath, long claimedSize = 1) =>
        new(propertyId, null, new DateOnly(2026, 1, 1), "Kvitto", "kvitto.pdf", "application/pdf", blobPath, claimedSize,
            DocumentCategory.Receipt);

    [Fact]
    public async Task UploadingBeyondTheDocumentLimit_IsRefusedBeforeTheUpload()
    {
        var (client, user, propertyId) = await CreatePropertyAsync();
        await SeedAsync(db =>
        {
            for (var i = 0; i < Limits.DocumentsPerProperty; i++)
            {
                db.Documents.Add(new Document
                {
                    PropertyId = propertyId, FileName = "f.pdf", ContentType = "application/pdf",
                    BlobPath = $"{propertyId}/{i}", UploadedByUserId = user.Id,
                });
            }
        });

        var response = await client.PostAsJsonAsync(
            "/api/documents/upload-url", new UploadUrlRequest(propertyId, "f.pdf", "application/pdf", 10));

        await AssertLimitReachedAsync(response);
    }

    [Fact]
    public async Task AnUploadUrlForAnObviouslyTooLargeFile_IsRefused()
    {
        var (client, _, propertyId) = await CreatePropertyAsync();

        var response = await client.PostAsJsonAsync(
            "/api/documents/upload-url", new UploadUrlRequest(propertyId, "film.mov", "video/quicktime", Limits.MaxFileBytes + 1));

        await AssertLimitReachedAsync(response);
    }

    [Fact]
    public async Task ABlobLargerThanTheFileLimit_IsRefusedAndDeleted_WhateverTheClaimedSize()
    {
        var (client, _, propertyId) = await CreatePropertyAsync();
        var blobPath = $"{propertyId}/{Guid.NewGuid()}-stor.pdf";
        _factory.Blob.Sizes[blobPath] = Limits.MaxFileBytes + 1;

        var response = await client.PostAsJsonAsync("/api/documents", CreateDocument(propertyId, blobPath, claimedSize: 1));

        await AssertLimitReachedAsync(response);
        Assert.Contains(blobPath, _factory.Blob.Deleted);
    }

    [Fact]
    public async Task TheStoredSize_IsWhatActuallyLanded_NotWhatTheClientClaimed()
    {
        var (client, _, propertyId) = await CreatePropertyAsync();
        var blobPath = $"{propertyId}/{Guid.NewGuid()}-kvitto.pdf";
        _factory.Blob.Sizes[blobPath] = 5000;

        var response = await client.PostAsJsonAsync("/api/documents", CreateDocument(propertyId, blobPath, claimedSize: 1));

        var document = await response.Content.ReadFromJsonAsync<DocumentDto>();
        Assert.Equal(5000, document!.SizeBytes);
    }

    [Fact]
    public async Task ExceedingThePropertysStorage_IsRefusedAndTheBlobDeleted()
    {
        var (client, user, propertyId) = await CreatePropertyAsync();
        await SeedAsync(db => db.Documents.Add(new Document
        {
            PropertyId = propertyId, FileName = "stort.zip", ContentType = "application/zip",
            BlobPath = $"{propertyId}/stort", SizeBytes = Limits.BlobStorageBytesPerProperty - 10,
            UploadedByUserId = user.Id,
        }));
        var blobPath = $"{propertyId}/{Guid.NewGuid()}-kvitto.pdf";

        var response = await client.PostAsJsonAsync("/api/documents", CreateDocument(propertyId, blobPath));

        await AssertLimitReachedAsync(response);
        Assert.Contains(blobPath, _factory.Blob.Deleted);
    }

    [Fact]
    public async Task RegisteringAnotherPropertysBlob_IsRefused()
    {
        // The hole this closes: a member of one property claiming another property's blob, then
        // asking for a download URL to it.
        var (client, _, propertyId) = await CreatePropertyAsync();
        var (_, _, otherPropertyId) = await CreatePropertyAsync();

        var response = await client.PostAsJsonAsync(
            "/api/documents", CreateDocument(propertyId, $"{otherPropertyId}/someone-elses.pdf"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
