using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClosedXML.Excel;
using HouseApp.Api.Data;
using HouseApp.Api.Dtos.Auth;
using HouseApp.Api.Export;
using HouseApp.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HouseApp.Api.Tests;

/// <summary>
/// The export is the promise that leaving the app costs nothing, so these check it actually contains
/// everything — and only what it should: names rather than ids, no Drive plumbing, no emails.
/// </summary>
public class PropertyExportTests : IClassFixture<HouseAppWebApplicationFactory>
{
    private readonly HouseAppWebApplicationFactory _factory;
    private static readonly PasswordHasher<ApplicationUser> Hasher = new();

    public PropertyExportTests(HouseAppWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<(HttpClient Client, ApplicationUser User)> CreateAuthenticatedClientAsync(string displayName = "Anna Ägare")
    {
        var user = new ApplicationUser
        {
            Email = $"{Guid.NewGuid()}@example.com",
            DisplayName = displayName,
            PasswordHash = string.Empty,
        };
        const string password = "Secret123!";
        user.PasswordHash = Hasher.HashPassword(user, password);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(user.Email, password));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        return (client, user);
    }

    private sealed record Seeded(string PropertyId, Project Project, Document BlobDocument, Document GeneralDocument, Document DriveDocument);

    private async Task<Seeded> SeedPropertyAsync(HttpClient client, ApplicationUser owner)
    {
        var dto = await TestData.CreatePropertyAsync(client, "Villa Åkerbo");
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var component = new PropertyComponent { Name = "Tak", RecommendedIntervalMonths = 360 };
        db.PropertyComponents.Add(component);

        var property = await db.Properties.FindAsync(dto.Id);
        property!.GoogleDriveFolderUrl = "https://drive.google.com/drive/folders/root-folder";
        property.GoogleDriveGeneralFolderId = "secret-general-folder-id";
        property.GoogleDriveProjectsFolderId = "secret-projects-folder-id";

        var project = new Project
        {
            PropertyId = dto.Id,
            Name = "Byte tak",
            WorkType = WorkType.Renovation,
            ComponentId = component.Id,
            Status = ProjectStatus.Completed,
            CompletedDate = new DateOnly(2025, 9, 1),
            EstimatedCost = 150_000m,
            Contractor = new ContractorInfo { Name = "Takfirman AB", Phone = "070-123" },
            Costs =
            [
                new ProjectCost { Type = CostType.Materials, Amount = 80_000m, DateIncurred = new DateOnly(2025, 8, 1) },
                new ProjectCost { Type = CostType.Labor, Amount = 60_000m, DateIncurred = new DateOnly(2025, 9, 1) },
            ],
            Milestones = [new ProjectMilestone { Description = "Rivning", CompletedDate = new DateOnly(2025, 8, 15) }],
            GoogleDriveFolderId = "secret-project-folder-id",
            CreatedByUserId = owner.Id,
            CreatedAt = new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero),
        };
        db.Projects.Add(project);

        db.ValuationEntries.Add(new ValuationEntry
        {
            PropertyId = dto.Id, Date = new DateOnly(2025, 1, 1), Value = 4_500_000m, Source = "Mäklare", CreatedByUserId = owner.Id,
        });
        db.Budgets.Add(new Budget { PropertyId = dto.Id, Year = 2025, MaintenanceBudget = 20_000m, PurchaseBudget = 5_000m });

        var blobDocument = new Document
        {
            PropertyId = dto.Id, ProjectId = project.Id, Date = new DateOnly(2025, 9, 2), Title = "Faktura tak",
            FileName = "scan_0042.pdf", ContentType = "application/pdf", BlobPath = $"{dto.Id}/blob-guid-scan_0042.pdf",
            Category = DocumentCategory.Invoice, UploadedByUserId = owner.Id,
        };
        var generalDocument = new Document
        {
            PropertyId = dto.Id, Date = new DateOnly(2020, 1, 1), Title = null, FileName = "lagfart.pdf",
            ContentType = "application/pdf", BlobPath = $"{dto.Id}/blob-guid-lagfart.pdf",
            Category = DocumentCategory.Deed, UploadedByUserId = owner.Id,
        };
        var driveDocument = new Document
        {
            PropertyId = dto.Id, Date = new DateOnly(2025, 3, 1), Title = "Garanti värmepump", FileName = "garanti.pdf",
            ContentType = "application/pdf", StorageKind = DocumentStorageKind.Drive, DriveFileId = "drive-file",
            DriveWebViewLink = "https://drive.google.com/file/d/drive-file/view", Category = DocumentCategory.Warranty,
            UploadedByUserId = owner.Id,
        };
        db.Documents.AddRange(blobDocument, generalDocument, driveDocument);
        await db.SaveChangesAsync();

        return new Seeded(dto.Id, project, blobDocument, generalDocument, driveDocument);
    }

    private static async Task<ZipArchive> GetExportAsync(HttpClient client, string propertyId)
    {
        var response = await client.GetAsync($"/api/properties/{propertyId}/export");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/zip", response.Content.Headers.ContentType?.MediaType);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        return new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
    }

    private static string ReadText(ZipArchive zip, string name)
    {
        var entry = zip.GetEntry(name);
        Assert.NotNull(entry);
        using var reader = new StreamReader(entry!.Open());
        return reader.ReadToEnd();
    }

    [Fact]
    public async Task Export_ContainsEveryKindOfData()
    {
        var (client, owner) = await CreateAuthenticatedClientAsync();
        var seeded = await SeedPropertyAsync(client, owner);

        using var zip = await GetExportAsync(client, seeded.PropertyId);
        using var json = JsonDocument.Parse(ReadText(zip, "data.json"));
        var root = json.RootElement;

        Assert.Equal(PropertyExportBuilder.FormatVersion, root.GetProperty("formatVersion").GetInt32());
        Assert.Equal("Anna Ägare", root.GetProperty("exportedBy").GetString());
        Assert.Equal("Villa Åkerbo", root.GetProperty("property").GetProperty("nickname").GetString());
        Assert.Equal(["Anna Ägare"], root.GetProperty("property").GetProperty("members").EnumerateArray().Select(m => m.GetString()));

        var project = Assert.Single(root.GetProperty("projects").EnumerateArray());
        Assert.Equal("Renovation", project.GetProperty("workType").GetString());
        Assert.Equal("Tak", project.GetProperty("componentName").GetString());
        Assert.Equal(140_000m, project.GetProperty("actualCost").GetDecimal());
        Assert.Equal(2, project.GetProperty("costs").GetArrayLength());
        Assert.Equal(1, project.GetProperty("milestones").GetArrayLength());
        Assert.Equal("Takfirman AB", project.GetProperty("contractor").GetProperty("name").GetString());
        Assert.Equal("Anna Ägare", project.GetProperty("createdBy").GetString());

        Assert.Equal(1, root.GetProperty("valuations").GetArrayLength());
        Assert.Equal(5_000m, root.GetProperty("budgets")[0].GetProperty("purchaseBudget").GetDecimal());
        Assert.Contains(root.GetProperty("components").EnumerateArray(), c => c.GetProperty("name").GetString() == "Tak");
        Assert.Equal(3, root.GetProperty("documents").GetArrayLength());
    }

    [Fact]
    public async Task Export_IncludesBlobFilesInTheAppsFolderLayout_AndLinksDriveFiles()
    {
        var (client, owner) = await CreateAuthenticatedClientAsync();
        var seeded = await SeedPropertyAsync(client, owner);

        using var zip = await GetExportAsync(client, seeded.PropertyId);
        using var json = JsonDocument.Parse(ReadText(zip, "data.json"));
        var documents = json.RootElement.GetProperty("documents").EnumerateArray().ToList();

        // Same tree DriveFolderResolver builds: a folder per project named "{name} {created date}".
        var projectFile = "dokument/Projekt/Byte tak 2025-06-01/2025-09-02 Faktura tak.pdf";
        var generalFile = "dokument/Allmänt/2020-01-01 lagfart.pdf";

        var blob = documents.Single(d => d.GetProperty("id").GetString() == seeded.BlobDocument.Id);
        Assert.Equal(projectFile, blob.GetProperty("file").GetString());
        Assert.Equal(generalFile, documents.Single(d => d.GetProperty("id").GetString() == seeded.GeneralDocument.Id)
            .GetProperty("file").GetString());

        using (var stream = zip.GetEntry(projectFile)!.Open())
        using (var copy = new MemoryStream())
        {
            stream.CopyTo(copy);
            Assert.Equal(FakeBlobStorageService.ContentFor(seeded.BlobDocument.BlobPath!), copy.ToArray());
        }
        Assert.NotNull(zip.GetEntry(generalFile));

        var drive = documents.Single(d => d.GetProperty("id").GetString() == seeded.DriveDocument.Id);
        Assert.Equal(JsonValueKind.Null, drive.GetProperty("file").ValueKind);
        Assert.Equal(seeded.DriveDocument.DriveWebViewLink, drive.GetProperty("driveWebViewLink").GetString());
        Assert.DoesNotContain(zip.Entries, e => e.FullName.Contains("garanti", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("Google Drive", ReadText(zip, "LÄSMIG.txt"));
    }

    [Fact]
    public async Task Export_ContainsAWorkbookWithOneSheetPerSubject()
    {
        var (client, owner) = await CreateAuthenticatedClientAsync();
        var seeded = await SeedPropertyAsync(client, owner);

        using var zip = await GetExportAsync(client, seeded.PropertyId);
        using var xlsx = new MemoryStream();
        using (var entry = zip.GetEntry("data.xlsx")!.Open())
        {
            entry.CopyTo(xlsx);
        }

        using var workbook = new XLWorkbook(xlsx);
        Assert.Equal(
            ["Fastighet", "Projekt", "Kostnader", "Milstolpar", "Värderingar", "Budget", "Dokument", "Komponenter"],
            workbook.Worksheets.Select(w => w.Name));

        var projects = workbook.Worksheet("Projekt");
        Assert.Equal("Byte tak", projects.Cell(2, 1).GetString());
        Assert.Equal("Renovering", projects.Cell(2, 3).GetString());
        Assert.Equal(3, workbook.Worksheet("Kostnader").LastRowUsed()!.RowNumber());
        Assert.Equal(4, workbook.Worksheet("Dokument").LastRowUsed()!.RowNumber());
    }

    [Fact]
    public async Task Export_LeavesOutIdsTokensAndEmails()
    {
        var (client, owner) = await CreateAuthenticatedClientAsync();
        var seeded = await SeedPropertyAsync(client, owner);

        using var zip = await GetExportAsync(client, seeded.PropertyId);
        var json = ReadText(zip, "data.json");

        Assert.DoesNotContain(owner.Id, json);
        Assert.DoesNotContain(owner.Email, json);
        Assert.DoesNotContain("secret-", json);
        Assert.DoesNotContain(seeded.BlobDocument.BlobPath!, json);
    }

    [Fact]
    public async Task Export_WithAMissingBlob_StillSucceedsAndSaysWhichFile()
    {
        var (client, owner) = await CreateAuthenticatedClientAsync();
        var seeded = await SeedPropertyAsync(client, owner);
        _factory.Blob.MissingPaths.Add(seeded.GeneralDocument.BlobPath!);

        using var zip = await GetExportAsync(client, seeded.PropertyId);
        using var json = JsonDocument.Parse(ReadText(zip, "data.json"));
        var documents = json.RootElement.GetProperty("documents").EnumerateArray().ToList();

        Assert.True(documents.Single(d => d.GetProperty("id").GetString() == seeded.GeneralDocument.Id)
            .GetProperty("fileMissing").GetBoolean());
        Assert.False(documents.Single(d => d.GetProperty("id").GetString() == seeded.BlobDocument.Id)
            .GetProperty("fileMissing").GetBoolean());
        Assert.Null(zip.GetEntry("dokument/Allmänt/2020-01-01 lagfart.pdf"));
        Assert.Contains("lagfart.pdf", ReadText(zip, "LÄSMIG.txt"));
    }

    [Fact]
    public async Task Export_IsNamedAfterThePropertyAndDate()
    {
        var (client, owner) = await CreateAuthenticatedClientAsync();
        var seeded = await SeedPropertyAsync(client, owner);

        var response = await client.GetAsync($"/api/properties/{seeded.PropertyId}/export");

        Assert.Equal("attachment", response.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Equal($"hustracker-villa-akerbo-{DateTime.UtcNow:yyyy-MM-dd}.zip", response.Content.Headers.ContentDisposition?.FileName);
    }

    [Fact]
    public async Task Export_DuplicateDocumentNames_GetNumbered()
    {
        var (client, owner) = await CreateAuthenticatedClientAsync();
        var dto = await TestData.CreatePropertyAsync(client);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            foreach (var n in new[] { 1, 2 })
            {
                db.Documents.Add(new Document
                {
                    PropertyId = dto.Id, Date = new DateOnly(2025, 1, 1), Title = "Kvitto: IKEA", FileName = "k.jpg",
                    ContentType = "image/jpeg", BlobPath = $"{dto.Id}/{n}", UploadedByUserId = owner.Id,
                    UploadedAt = DateTimeOffset.UtcNow.AddMinutes(n),
                });
            }
            await db.SaveChangesAsync();
        }

        using var zip = await GetExportAsync(client, dto.Id);

        // ':' is replaced too — the zip gets unpacked on Windows as well.
        Assert.NotNull(zip.GetEntry("dokument/Allmänt/2025-01-01 Kvitto_ IKEA.jpg"));
        Assert.NotNull(zip.GetEntry("dokument/Allmänt/2025-01-01 Kvitto_ IKEA (2).jpg"));
    }

    [Fact]
    public void ExportLabels_CoverEveryEnumValue()
    {
        // An appended enum value must get a Swedish label before it can reach a spreadsheet.
        Assert.All(Enum.GetValues<WorkType>(), v => Assert.True(ExportLabels.WorkTypes.ContainsKey(v), v.ToString()));
        Assert.All(Enum.GetValues<ProjectStatus>(), v => Assert.True(ExportLabels.Statuses.ContainsKey(v), v.ToString()));
        Assert.All(Enum.GetValues<ProjectPriority>(), v => Assert.True(ExportLabels.Priorities.ContainsKey(v), v.ToString()));
        Assert.All(Enum.GetValues<CostType>(), v => Assert.True(ExportLabels.CostTypes.ContainsKey(v), v.ToString()));
        Assert.All(Enum.GetValues<PropertyType>(), v => Assert.True(ExportLabels.PropertyTypes.ContainsKey(v), v.ToString()));
        Assert.All(Enum.GetValues<DocumentCategory>(), v => Assert.True(ExportLabels.DocumentCategories.ContainsKey(v), v.ToString()));
        Assert.All(Enum.GetValues<DocumentStorageKind>(), v => Assert.True(ExportLabels.StorageKinds.ContainsKey(v), v.ToString()));
    }
}
