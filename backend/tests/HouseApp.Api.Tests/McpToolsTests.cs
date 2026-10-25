using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HouseApp.Api.Dtos.Projects;
using HouseApp.Api.Models;
using ModelContextProtocol.Client;
using Xunit;

namespace HouseApp.Api.Tests;

/// <summary>
/// What Claude can and can't do once connected. The rules that matter most: only the user's own
/// houses (never the demo, whose content strangers write), nothing deleted, partial updates that
/// leave everything else alone, and the same limits and Drive behaviour as the app.
/// </summary>
public class McpToolsTests : IClassFixture<HouseAppWebApplicationFactory>
{
    private readonly HouseAppWebApplicationFactory _factory;
    private readonly McpTestHelper _mcp;

    public McpToolsTests(HouseAppWebApplicationFactory factory)
    {
        _factory = factory;
        _mcp = new McpTestHelper(factory);
        _factory.Drive.ConnectionExpired = false;
    }

    private record Connected(McpTestHelper.SignedIn SignedIn, McpClient Client, string PropertyId, string ComponentId);

    private async Task<Connected> ConnectWithPropertyAsync()
    {
        var signedIn = await _mcp.SignInAsync();
        var property = await TestData.CreatePropertyAsync(signedIn.Browser, "Villa Åkerbo");
        var component = new PropertyComponent { Name = $"Tak {Guid.NewGuid():N}", RecommendedIntervalMonths = 360 };
        await _mcp.SeedAsync(db => db.PropertyComponents.Add(component));
        var tokens = await _mcp.ConnectAsync(signedIn);
        return new Connected(signedIn, await _mcp.McpClientAsync(tokens.AccessToken), property.Id, component.Id);
    }

    private static Task<(bool IsError, JsonElement Json, string Text)> Call(Connected c, string tool, Dictionary<string, object?> args) =>
        McpTestHelper.CallAsync(c.Client, tool, args);

    private async Task<string> CreateProjectAsync(Connected c, string name = "Byte tak")
    {
        var (isError, json, text) = await Call(c, "create_project", new()
        {
            ["propertyId"] = c.PropertyId,
            ["name"] = name,
            ["workType"] = "Renovation",
            ["componentId"] = c.ComponentId,
            ["estimatedCost"] = 150000,
            ["notes"] = "Ursprunglig anteckning",
        });
        Assert.False(isError, text);
        return json.GetProperty("id").GetString()!;
    }

    // --- Which houses ------------------------------------------------------------------------------

    [Fact]
    public async Task OnlyTheUsersOwnHouses_AreVisible_NeverTheDemo()
    {
        var c = await ConnectWithPropertyAsync();
        var demo = new Property { Nickname = "Demo", Address = "Demogatan 1", IsDemo = true, MemberUserIds = [c.SignedIn.User.Id] };
        var someoneElses = new Property { Nickname = "Grannens", Address = "Granngatan 2", MemberUserIds = ["someone-else"] };
        await _mcp.SeedAsync(db => db.Properties.AddRange(demo, someoneElses));

        var (_, json, _) = await Call(c, "list_properties", new());
        var ids = json.EnumerateArray().Select(p => p.GetProperty("id").GetString()).ToList();
        Assert.Equal([c.PropertyId], ids);

        // Not just hidden from the list: unreachable by id, even for a member of the demo.
        Assert.True((await Call(c, "list_projects", new() { ["propertyId"] = demo.Id })).IsError);
        Assert.True((await Call(c, "list_projects", new() { ["propertyId"] = someoneElses.Id })).IsError);
    }

    // --- Projects -----------------------------------------------------------------------------------

    [Fact]
    public async Task CreateProject_ShowsUpInTheApp()
    {
        var c = await ConnectWithPropertyAsync();
        var projectId = await CreateProjectAsync(c);

        var project = await c.SignedIn.Browser.GetFromJsonAsync<ProjectDto>($"/api/projects/{projectId}?propertyId={c.PropertyId}");
        Assert.Equal("Byte tak", project!.Name);
        Assert.Equal(WorkType.Renovation, project.WorkType);
        Assert.Equal(c.SignedIn.User.Id, project.CreatedByUserId);
    }

    [Fact]
    public async Task CreateProject_WithAComponentFromElsewhere_IsRefused()
    {
        var c = await ConnectWithPropertyAsync();

        var (isError, _, text) = await Call(c, "create_project", new()
        {
            ["propertyId"] = c.PropertyId,
            ["name"] = "Okänt",
            ["workType"] = "Maintenance",
            ["componentId"] = "no-such-component",
        });

        Assert.True(isError);
        Assert.Contains("list_components", text);
    }

    [Fact]
    public async Task UpdateProject_ChangesOnlyWhatIsPassed()
    {
        var c = await ConnectWithPropertyAsync();
        var projectId = await CreateProjectAsync(c);
        await Call(c, "add_project_cost", new()
        {
            ["propertyId"] = c.PropertyId, ["projectId"] = projectId, ["type"] = "Materials",
            ["amount"] = 1000, ["dateIncurred"] = "2026-05-01",
        });

        var (isError, json, text) = await Call(c, "update_project", new()
        {
            ["propertyId"] = c.PropertyId, ["projectId"] = projectId, ["status"] = "InProgress",
        });

        Assert.False(isError, text);
        Assert.Equal("InProgress", json.GetProperty("status").GetString());
        Assert.Equal("Byte tak", json.GetProperty("name").GetString());
        Assert.Equal("Ursprunglig anteckning", json.GetProperty("notes").GetString());
        Assert.Equal(150000m, json.GetProperty("estimatedCost").GetDecimal());
        Assert.Equal(1, json.GetProperty("costs").GetArrayLength());
    }

    [Fact]
    public async Task AddedCosts_CountTowardTheBudget()
    {
        var c = await ConnectWithPropertyAsync();
        var projectId = await CreateProjectAsync(c);

        await Call(c, "add_project_cost", new()
        {
            ["propertyId"] = c.PropertyId, ["projectId"] = projectId, ["type"] = "Labor",
            ["amount"] = 42000, ["dateIncurred"] = "2026-03-15",
        });
        var (_, budget, _) = await Call(c, "get_budget", new() { ["propertyId"] = c.PropertyId, ["year"] = 2026 });

        var renovation = budget.GetProperty("lines").EnumerateArray().Single(l => l.GetProperty("workType").GetString() == "Renovation");
        Assert.Equal(42000m, renovation.GetProperty("spent").GetDecimal());
    }

    [Fact]
    public async Task Milestones_CanBeAddedAndCompleted_KeepingTheirIds()
    {
        var c = await ConnectWithPropertyAsync();
        var projectId = await CreateProjectAsync(c);

        var (_, added, _) = await Call(c, "add_project_milestone", new()
        {
            ["propertyId"] = c.PropertyId, ["projectId"] = projectId, ["description"] = "Riva gamla taket",
        });
        var milestoneId = added.GetProperty("milestones")[0].GetProperty("id").GetString();

        // An unrelated edit in between must not invalidate the id Claude was just given.
        await Call(c, "update_project", new() { ["propertyId"] = c.PropertyId, ["projectId"] = projectId, ["priority"] = "High" });

        var (isError, done, text) = await Call(c, "update_milestone", new()
        {
            ["propertyId"] = c.PropertyId, ["projectId"] = projectId, ["milestoneId"] = milestoneId,
            ["completedDate"] = "2026-06-01",
        });
        Assert.False(isError, text);
        Assert.Equal("2026-06-01", done.GetProperty("milestones")[0].GetProperty("completedDate").GetString());
    }

    [Fact]
    public async Task TheProjectLimit_ComesBackAsAToolError()
    {
        var c = await ConnectWithPropertyAsync();
        await _mcp.SeedAsync(db =>
        {
            for (var i = 0; i < Limits.ProjectsPerProperty; i++)
            {
                db.Projects.Add(new Project { PropertyId = c.PropertyId, Name = $"P{i}", ComponentId = c.ComponentId, CreatedByUserId = "x" });
            }
        });

        var (isError, _, text) = await Call(c, "create_project", new()
        {
            ["propertyId"] = c.PropertyId, ["name"] = "En för mycket", ["workType"] = "Maintenance", ["componentId"] = c.ComponentId,
        });

        Assert.True(isError);
        Assert.Contains("högst", text);
    }

    // --- Documents ----------------------------------------------------------------------------------

    private async Task<Document> SeedDocumentAsync(string propertyId, string? title = "Kvitto", string? driveFileId = null)
    {
        var document = new Document
        {
            PropertyId = propertyId,
            Title = title,
            FileName = "scan_0042.pdf",
            ContentType = "application/pdf",
            Date = new DateOnly(2026, 1, 1),
            Category = DocumentCategory.Receipt,
            StorageKind = driveFileId is null ? DocumentStorageKind.Blob : DocumentStorageKind.Drive,
            BlobPath = driveFileId is null ? $"{propertyId}/scan" : null,
            DriveFileId = driveFileId,
            UploadedByUserId = "x",
        };
        await _mcp.SeedAsync(db => db.Documents.Add(document));
        return document;
    }

    [Fact]
    public async Task UpdateDocument_IsPartial_AndATitleIsRequired()
    {
        var c = await ConnectWithPropertyAsync();
        var document = await SeedDocumentAsync(c.PropertyId);

        var (isError, json, text) = await Call(c, "update_document", new()
        {
            ["propertyId"] = c.PropertyId, ["documentId"] = document.Id, ["category"] = "Invoice",
        });
        Assert.False(isError, text);
        Assert.Equal("Invoice", json.GetProperty("category").GetString());
        Assert.Equal("Kvitto", json.GetProperty("title").GetString());

        var untitled = await SeedDocumentAsync(c.PropertyId, title: null);
        var (refused, _, _) = await Call(c, "update_document", new()
        {
            ["propertyId"] = c.PropertyId, ["documentId"] = untitled.Id, ["category"] = "Invoice",
        });
        Assert.True(refused);
    }

    [Fact]
    public async Task SetDocumentProject_OnDrive_MovesTheFile()
    {
        var c = await ConnectWithPropertyAsync();
        var connect = await c.SignedIn.Browser.GetAsync($"/api/drive/connect?propertyId={c.PropertyId}");
        var state = Uri.UnescapeDataString(connect.Headers.Location!.ToString().Split("state=")[1]);
        await c.SignedIn.Browser.GetAsync($"/api/drive/callback?code=fake-code&state={Uri.EscapeDataString(state)}");
        var projectId = await CreateProjectAsync(c);
        var fileId = $"drive-{Guid.NewGuid():N}";
        _factory.Drive.Files[fileId] = "somewhere";
        var document = await SeedDocumentAsync(c.PropertyId, driveFileId: fileId);

        var (isError, json, text) = await Call(c, "set_document_project", new()
        {
            ["propertyId"] = c.PropertyId, ["documentId"] = document.Id, ["projectId"] = projectId,
        });

        Assert.False(isError, text);
        Assert.Equal(projectId, json.GetProperty("projectId").GetString());
        Assert.StartsWith("Byte tak", _factory.Drive.FolderNameOf(_factory.Drive.Files[fileId]));
    }

    [Fact]
    public async Task SetDocumentProject_WithAnExpiredDrive_SaysToRenew()
    {
        var c = await ConnectWithPropertyAsync();
        var connect = await c.SignedIn.Browser.GetAsync($"/api/drive/connect?propertyId={c.PropertyId}");
        var state = Uri.UnescapeDataString(connect.Headers.Location!.ToString().Split("state=")[1]);
        await c.SignedIn.Browser.GetAsync($"/api/drive/callback?code=fake-code&state={Uri.EscapeDataString(state)}");
        var projectId = await CreateProjectAsync(c);
        var document = await SeedDocumentAsync(c.PropertyId, driveFileId: $"drive-{Guid.NewGuid():N}");

        _factory.Drive.ConnectionExpired = true;
        try
        {
            var (isError, _, text) = await Call(c, "set_document_project", new()
            {
                ["propertyId"] = c.PropertyId, ["documentId"] = document.Id, ["projectId"] = projectId,
            });
            Assert.True(isError);
            Assert.Contains("renew", text);
        }
        finally
        {
            _factory.Drive.ConnectionExpired = false;
        }
    }

    [Fact]
    public async Task SetDocumentProject_ToAnotherHousesProject_IsRefused()
    {
        var c = await ConnectWithPropertyAsync();
        var document = await SeedDocumentAsync(c.PropertyId);

        var (isError, _, _) = await Call(c, "set_document_project", new()
        {
            ["propertyId"] = c.PropertyId, ["documentId"] = document.Id, ["projectId"] = "someone-elses-project",
        });

        Assert.True(isError);
    }

    [Fact]
    public async Task UploadLink_PointsAtTheProject_WithTheSuggestions()
    {
        var c = await ConnectWithPropertyAsync();
        var projectId = await CreateProjectAsync(c);

        var (_, json, _) = await Call(c, "create_upload_link", new()
        {
            ["propertyId"] = c.PropertyId, ["projectId"] = projectId,
            ["title"] = "Offert Takfirman & Co", ["category"] = "Quote", ["date"] = "2026-04-02",
        });

        Assert.Equal(
            $"http://localhost/properties/{c.PropertyId}/projects/{projectId}" +
            "?upload=1&title=Offert%20Takfirman%20%26%20Co&category=Quote&date=2026-04-02",
            json.GetProperty("url").GetString());
    }

    [Fact]
    public async Task UploadLink_ForAnotherHousesProject_IsRefused()
    {
        var c = await ConnectWithPropertyAsync();

        var (isError, _, _) = await Call(c, "create_upload_link", new()
        {
            ["propertyId"] = c.PropertyId, ["projectId"] = "not-in-this-house",
        });

        Assert.True(isError);
    }
}
