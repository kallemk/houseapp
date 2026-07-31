using System.Net;
using System.Net.Http.Json;
using HouseApp.Api.Data;
using HouseApp.Api.Dtos.Auth;
using HouseApp.Api.Dtos.Documents;
using HouseApp.Api.Dtos.Projects;
using HouseApp.Api.Dtos.Properties;
using HouseApp.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HouseApp.Api.Tests;

/// <summary>
/// Documents can live in Blob Storage or in a property owner's Google Drive. Most of what can go
/// wrong is at the seam: a property switching backends, a client using the wrong upload path, or a
/// grant that has stopped working.
///
/// The OAuth round trip is driven for real here — /connect is called, the protected state is taken
/// out of the redirect, and /callback is handed back — so the state protection and the controller
/// logic are under test. Only Google itself is faked.
/// </summary>
public class GoogleDriveDocumentsTests : IClassFixture<HouseAppWebApplicationFactory>
{
    private readonly HouseAppWebApplicationFactory _factory;
    private static readonly PasswordHasher<ApplicationUser> Hasher = new();

    public GoogleDriveDocumentsTests(HouseAppWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.Drive.ConnectionExpired = false;
        _factory.Drive.RefreshTokenToIssue = "fake-refresh-token";
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
                DisplayName = "Drive Tester",
                PasswordHash = string.Empty,
                IsAdmin = true,
            };
            user.PasswordHash = Hasher.HashPassword(user, password);
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }

        // Redirects must not be followed: /connect points at Google, which isn't running here, and
        // the callback's redirect back into the SPA is itself what several tests assert on.
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        return client;
    }

    /// <summary>Walks the real connect flow, pulling the protected state out of the consent redirect.</summary>
    private static async Task<HttpResponseMessage> ConnectDriveAsync(HttpClient client, string propertyId)
    {
        var connect = await client.GetAsync($"/api/drive/connect?propertyId={propertyId}");
        Assert.Equal(HttpStatusCode.Redirect, connect.StatusCode);

        var state = StateFrom(connect.Headers.Location!.ToString());
        return await client.GetAsync($"/api/drive/callback?code=fake-code&state={Uri.EscapeDataString(state)}");
    }

    /// <summary>FakeGoogleDriveService puts state last and alone, so this stays a one-liner.</summary>
    private static string StateFrom(string consentUrl) =>
        Uri.UnescapeDataString(consentUrl.Split("state=")[1]);

    private static async Task<PropertyDto> GetPropertyAsync(HttpClient client, string propertyId) =>
        (await (await client.GetAsync($"/api/properties/{propertyId}")).Content.ReadFromJsonAsync<PropertyDto>())!;

    private static MultipartFormDataContent DriveUpload(
        string propertyId,
        string title = "Besiktning",
        string fileName = "besiktning.pdf",
        string? projectId = null)
    {
        var content = new MultipartFormDataContent
        {
            { new StringContent(propertyId), "propertyId" },
            { new StringContent("2026-03-01"), "date" },
            { new StringContent(title), "title" },
            { new StringContent(nameof(DocumentCategory.Other)), "category" },
            { new ByteArrayContent([1, 2, 3, 4]), "file", fileName },
        };
        if (projectId is not null)
        {
            content.Add(new StringContent(projectId), "projectId");
        }

        return content;
    }

    [Fact]
    public async Task ANewProperty_StoresDocumentsInBlob()
    {
        var client = await CreateAuthenticatedClientAsync();
        var property = await TestData.CreatePropertyAsync(client);

        Assert.Equal(DocumentStorageKind.Blob, (await GetPropertyAsync(client, property.Id)).DocumentStorage);

        var mode = await (await client.PostAsJsonAsync("/api/documents/upload-url",
            new UploadUrlRequest(property.Id, "kvitto.pdf", "application/pdf")))
            .Content.ReadFromJsonAsync<UploadUrlResponse>();

        Assert.Equal(UploadMode.Sas, mode!.Mode);
        Assert.NotNull(mode.UploadUrl);
    }

    [Fact]
    public async Task Connecting_CreatesAFolderAndSwitchesTheProperty()
    {
        var client = await CreateAuthenticatedClientAsync();
        var property = await TestData.CreatePropertyAsync(client, "Villa Drive");

        var callback = await ConnectDriveAsync(client, property.Id);

        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
        Assert.Contains("drive=connected", callback.Headers.Location!.ToString());
        Assert.Contains(_factory.Drive.Folders.Values, f => f.Name.Contains("Villa Drive"));

        var updated = await GetPropertyAsync(client, property.Id);
        Assert.Equal(DocumentStorageKind.Drive, updated.DocumentStorage);
        Assert.NotNull(updated.DriveFolderUrl);
        Assert.Equal("Drive Tester", updated.DriveConnectedByName);
    }

    [Fact]
    public async Task Connecting_CreatesTheAllmantAndProjektSubfolders()
    {
        var client = await CreateAuthenticatedClientAsync();
        var property = await TestData.CreatePropertyAsync(client, "Villa Struktur");
        await ConnectDriveAsync(client, property.Id);

        var root = _factory.Drive.Folders
            .First(f => f.Value.Name.Contains("Villa Struktur")).Key;

        // Both made up front, so the folder looks organised the moment it's opened in Drive.
        Assert.NotNull(_factory.Drive.FolderIdIn(root, "Allmänt"));
        Assert.NotNull(_factory.Drive.FolderIdIn(root, "Projekt"));
    }

    [Fact]
    public async Task ADocumentWithNoProject_GoesToAllmant()
    {
        var client = await CreateAuthenticatedClientAsync();
        var property = await TestData.CreatePropertyAsync(client, "Villa Allmän");
        await ConnectDriveAsync(client, property.Id);

        await client.PostAsync("/api/documents/upload", DriveUpload(property.Id));

        var root = _factory.Drive.Folders.First(f => f.Value.Name.Contains("Villa Allmän")).Key;
        var general = _factory.Drive.FolderIdIn(root, "Allmänt");
        Assert.Contains(_factory.Drive.Files.Values, folderId => folderId == general);
    }

    [Fact]
    public async Task ADocumentOnAProject_GoesToAProjectFolderDatedWhenTheProjectWasCreated()
    {
        var client = await CreateAuthenticatedClientAsync();
        var property = await TestData.CreatePropertyAsync(client, "Villa Projekt");
        await ConnectDriveAsync(client, property.Id);

        var componentId = Guid.NewGuid().ToString();
        var project = (await (await client.PostAsJsonAsync(
                $"/api/properties/{property.Id}/projects",
                TestData.SaveProject("Nytt tak", componentId)))
            .Content.ReadFromJsonAsync<ProjectDto>())!;

        await client.PostAsync("/api/documents/upload", DriveUpload(property.Id, projectId: project.Id));

        var root = _factory.Drive.Folders.First(f => f.Value.Name.Contains("Villa Projekt")).Key;
        var projectsFolder = _factory.Drive.FolderIdIn(root, "Projekt");
        var expectedName = $"Nytt tak {project.CreatedAt:yyyy-MM-dd}";
        var projectFolder = _factory.Drive.FolderIdIn(projectsFolder, expectedName);

        Assert.NotNull(projectFolder);
        Assert.Contains(_factory.Drive.Files.Values, folderId => folderId == projectFolder);
    }

    [Fact]
    public async Task ASecondDocumentOnTheSameProject_ReusesItsFolder()
    {
        var client = await CreateAuthenticatedClientAsync();
        var property = await TestData.CreatePropertyAsync(client, "Villa Återbruk");
        await ConnectDriveAsync(client, property.Id);
        var project = (await (await client.PostAsJsonAsync(
                $"/api/properties/{property.Id}/projects",
                TestData.SaveProject("Fasadmålning", Guid.NewGuid().ToString())))
            .Content.ReadFromJsonAsync<ProjectDto>())!;

        await client.PostAsync("/api/documents/upload", DriveUpload(property.Id, "Offert", projectId: project.Id));
        var foldersAfterFirst = _factory.Drive.Folders.Count;
        await client.PostAsync("/api/documents/upload", DriveUpload(property.Id, "Faktura", projectId: project.Id));

        Assert.Equal(foldersAfterFirst, _factory.Drive.Folders.Count);
    }

    [Fact]
    public async Task AnUnknownProjectId_FilesUnderAllmantRatherThanFailing()
    {
        // Better to store the file somewhere sensible than to lose the upload over its filing.
        var client = await CreateAuthenticatedClientAsync();
        var property = await TestData.CreatePropertyAsync(client, "Villa Okänd");
        await ConnectDriveAsync(client, property.Id);

        var response = await client.PostAsync(
            "/api/documents/upload",
            DriveUpload(property.Id, projectId: "no-such-project"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var root = _factory.Drive.Folders.First(f => f.Value.Name.Contains("Villa Okänd")).Key;
        Assert.Contains(_factory.Drive.Files.Values, id => id == _factory.Drive.FolderIdIn(root, "Allmänt"));
    }

    [Fact]
    public async Task AttachingADocumentToAProjectAfterUpload_MovesTheFile()
    {
        var client = await CreateAuthenticatedClientAsync();
        var property = await TestData.CreatePropertyAsync(client, "Villa Flytt");
        await ConnectDriveAsync(client, property.Id);
        var project = (await (await client.PostAsJsonAsync(
                $"/api/properties/{property.Id}/projects",
                TestData.SaveProject("Nytt kök", Guid.NewGuid().ToString())))
            .Content.ReadFromJsonAsync<ProjectDto>())!;

        // Uploaded loose, then attached — the path that used to leave the file behind in "Allmänt".
        var document = (await (await client.PostAsync("/api/documents/upload", DriveUpload(property.Id)))
            .Content.ReadFromJsonAsync<DocumentDto>())!;
        var root = _factory.Drive.Folders.First(f => f.Value.Name.Contains("Villa Flytt")).Key;
        var general = _factory.Drive.FolderIdIn(root, "Allmänt");
        var fileId = _factory.Drive.Files.Single(f => f.Value == general).Key;

        var attach = await client.PutAsJsonAsync(
            $"/api/documents/{document.Id}/project?propertyId={property.Id}",
            new SetDocumentProjectRequest(project.Id));
        Assert.Equal(HttpStatusCode.NoContent, attach.StatusCode);

        var projectsFolder = _factory.Drive.FolderIdIn(root, "Projekt");
        var projectFolder = _factory.Drive.FolderIdIn(projectsFolder, $"Nytt kök {project.CreatedAt:yyyy-MM-dd}");
        Assert.Equal(projectFolder, _factory.Drive.Files[fileId]);

        // And detaching puts it back.
        await client.PutAsJsonAsync(
            $"/api/documents/{document.Id}/project?propertyId={property.Id}",
            new SetDocumentProjectRequest(null));
        Assert.Equal(general, _factory.Drive.Files[fileId]);
    }

    [Fact]
    public async Task RenamingAProject_RenamesItsDriveFolder()
    {
        var client = await CreateAuthenticatedClientAsync();
        var property = await TestData.CreatePropertyAsync(client, "Villa Namn");
        await ConnectDriveAsync(client, property.Id);
        var project = (await (await client.PostAsJsonAsync(
                $"/api/properties/{property.Id}/projects",
                TestData.SaveProject("Gammalt namn", Guid.NewGuid().ToString())))
            .Content.ReadFromJsonAsync<ProjectDto>())!;
        await client.PostAsync("/api/documents/upload", DriveUpload(property.Id, projectId: project.Id));

        var root = _factory.Drive.Folders.First(f => f.Value.Name.Contains("Villa Namn")).Key;
        var projectsFolder = _factory.Drive.FolderIdIn(root, "Projekt");
        var projectFolder = _factory.Drive.Folders.Single(f => f.Value.ParentId == projectsFolder).Key;

        var rename = await client.PutAsJsonAsync(
            $"/api/projects/{project.Id}?propertyId={property.Id}",
            TestData.SaveProject("Nytt namn", project.ComponentId));
        Assert.Equal(HttpStatusCode.NoContent, rename.StatusCode);

        // Same folder, new name — the date suffix stays the project's creation date.
        Assert.Equal($"Nytt namn {project.CreatedAt:yyyy-MM-dd}", _factory.Drive.FolderNameOf(projectFolder));
    }

    [Fact]
    public async Task RenamingAProject_StillSucceedsWhenDriveIsUnreachable()
    {
        // A folder name is cosmetic and the files inside it are already right, so a dead grant must
        // not block renaming a project.
        var client = await CreateAuthenticatedClientAsync();
        var property = await TestData.CreatePropertyAsync(client, "Villa Trasig");
        await ConnectDriveAsync(client, property.Id);
        var project = (await (await client.PostAsJsonAsync(
                $"/api/properties/{property.Id}/projects",
                TestData.SaveProject("Före", Guid.NewGuid().ToString())))
            .Content.ReadFromJsonAsync<ProjectDto>())!;
        await client.PostAsync("/api/documents/upload", DriveUpload(property.Id, projectId: project.Id));

        _factory.Drive.ConnectionExpired = true;
        var rename = await client.PutAsJsonAsync(
            $"/api/projects/{project.Id}?propertyId={property.Id}",
            TestData.SaveProject("Efter", project.ComponentId));

        Assert.Equal(HttpStatusCode.NoContent, rename.StatusCode);
        var updated = await (await client.GetAsync($"/api/properties/{property.Id}/projects"))
            .Content.ReadFromJsonAsync<List<ProjectDto>>();
        Assert.Equal("Efter", updated!.Single(p => p.Id == project.Id).Name);
    }

    [Fact]
    public async Task AttachingADocument_RefusesWhenDriveIsUnreachable()
    {
        // The opposite call: here the file really would end up misfiled, so the attachment is
        // refused rather than recorded against a file sitting somewhere else.
        var client = await CreateAuthenticatedClientAsync();
        var property = await TestData.CreatePropertyAsync(client, "Villa Vägrar");
        await ConnectDriveAsync(client, property.Id);
        var project = (await (await client.PostAsJsonAsync(
                $"/api/properties/{property.Id}/projects",
                TestData.SaveProject("Tak", Guid.NewGuid().ToString())))
            .Content.ReadFromJsonAsync<ProjectDto>())!;
        var document = (await (await client.PostAsync("/api/documents/upload", DriveUpload(property.Id)))
            .Content.ReadFromJsonAsync<DocumentDto>())!;

        _factory.Drive.ConnectionExpired = true;
        var attach = await client.PutAsJsonAsync(
            $"/api/documents/{document.Id}/project?propertyId={property.Id}",
            new SetDocumentProjectRequest(project.Id));

        Assert.Equal(HttpStatusCode.Conflict, attach.StatusCode);

        // And nothing was recorded, so the two sides still agree.
        _factory.Drive.ConnectionExpired = false;
        var documents = await (await client.GetAsync($"/api/properties/{property.Id}/documents"))
            .Content.ReadFromJsonAsync<List<DocumentDto>>();
        Assert.Null(documents!.Single(d => d.Id == document.Id).ProjectId);
    }

    [Fact]
    public async Task Disconnecting_ForgetsTheProjectFoldersToo()
    {
        // They point into a tree that's being forgotten; reconnecting builds a fresh one, and stale
        // ids would file new documents outside it where nobody would look.
        var client = await CreateAuthenticatedClientAsync();
        var property = await TestData.CreatePropertyAsync(client, "Villa Om");
        await ConnectDriveAsync(client, property.Id);
        var project = (await (await client.PostAsJsonAsync(
                $"/api/properties/{property.Id}/projects",
                TestData.SaveProject("Dränering", Guid.NewGuid().ToString())))
            .Content.ReadFromJsonAsync<ProjectDto>())!;
        await client.PostAsync("/api/documents/upload", DriveUpload(property.Id, projectId: project.Id));

        await client.DeleteAsync($"/api/drive/connection?propertyId={property.Id}");
        await ConnectDriveAsync(client, property.Id);
        await client.PostAsync("/api/documents/upload", DriveUpload(property.Id, "Efter", projectId: project.Id));

        // Reconnecting made a second root, so the old one can't be told apart by shape — both have a
        // populated "Projekt". The property points at the current one.
        var newRoot = (await GetPropertyAsync(client, property.Id)).DriveFolderUrl!.Split('/').Last();
        Assert.Equal(2, _factory.Drive.Folders.Count(f => f.Value.Name.Contains("Villa Om")));

        var newProjectsFolder = _factory.Drive.FolderIdIn(newRoot, "Projekt");
        Assert.NotNull(newProjectsFolder);

        // A fresh project folder under the *new* tree, and the second upload inside it.
        var newProjectFolder = _factory.Drive.Folders
            .Single(f => f.Value.ParentId == newProjectsFolder).Key;
        Assert.Contains(_factory.Drive.Files.Values, folderId => folderId == newProjectFolder);
    }

    [Fact]
    public async Task AConnectedProperty_RoutesUploadsToDrive()
    {
        var client = await CreateAuthenticatedClientAsync();
        var property = await TestData.CreatePropertyAsync(client);
        await ConnectDriveAsync(client, property.Id);

        var mode = await (await client.PostAsJsonAsync("/api/documents/upload-url",
            new UploadUrlRequest(property.Id, "kvitto.pdf", "application/pdf")))
            .Content.ReadFromJsonAsync<UploadUrlResponse>();
        Assert.Equal(UploadMode.Drive, mode!.Mode);
        Assert.Null(mode.UploadUrl);

        // Counted as a delta, not an absolute: the fake is shared across this class's tests, which
        // xUnit may run in any order.
        var filesBefore = _factory.Drive.Files.Count;
        var response = await client.PostAsync("/api/documents/upload", DriveUpload(property.Id));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var document = (await response.Content.ReadFromJsonAsync<DocumentDto>())!;
        Assert.Equal(DocumentStorageKind.Drive, document.StorageKind);
        Assert.NotNull(document.DriveWebViewLink);
        Assert.Equal(filesBefore + 1, _factory.Drive.Files.Count);
    }

    [Fact]
    public async Task EachUploadPath_RefusesAPropertyOnTheOtherBackend()
    {
        // Guards against a client working from a stale cache writing a row that points at a file
        // nobody ever uploaded.
        var client = await CreateAuthenticatedClientAsync();
        var blobProperty = await TestData.CreatePropertyAsync(client, "Blob");
        var driveProperty = await TestData.CreatePropertyAsync(client, "Drive");
        await ConnectDriveAsync(client, driveProperty.Id);

        var blobRowOnDriveProperty = await client.PostAsJsonAsync("/api/documents",
            new CreateDocumentRequest(driveProperty.Id, null, new DateOnly(2026, 1, 1), "Smyg", "s.pdf",
                "application/pdf", "path", 1, DocumentCategory.Other));
        Assert.Equal(HttpStatusCode.Conflict, blobRowOnDriveProperty.StatusCode);

        var driveUploadOnBlobProperty = await client.PostAsync("/api/documents/upload", DriveUpload(blobProperty.Id));
        Assert.Equal(HttpStatusCode.Conflict, driveUploadOnBlobProperty.StatusCode);
    }

    [Fact]
    public async Task DownloadUrl_ForADriveDocument_IsTheLinkStoredAtUpload()
    {
        // No Drive call and no live connection needed to open a document.
        var client = await CreateAuthenticatedClientAsync();
        var property = await TestData.CreatePropertyAsync(client);
        await ConnectDriveAsync(client, property.Id);
        var document = (await (await client.PostAsync("/api/documents/upload", DriveUpload(property.Id)))
            .Content.ReadFromJsonAsync<DocumentDto>())!;

        var url = await (await client.GetAsync($"/api/documents/{document.Id}/download-url?propertyId={property.Id}"))
            .Content.ReadFromJsonAsync<DownloadUrlResponse>();

        Assert.Equal(document.DriveWebViewLink, url!.DownloadUrl);
    }

    [Fact]
    public async Task Deleting_LeavesTheDriveFileAlone_UnlessAsked()
    {
        var client = await CreateAuthenticatedClientAsync();
        var property = await TestData.CreatePropertyAsync(client);
        await ConnectDriveAsync(client, property.Id);

        var kept = (await (await client.PostAsync("/api/documents/upload", DriveUpload(property.Id, "Behålls")))
            .Content.ReadFromJsonAsync<DocumentDto>())!;
        var purged = (await (await client.PostAsync("/api/documents/upload", DriveUpload(property.Id, "Rensas")))
            .Content.ReadFromJsonAsync<DocumentDto>())!;
        var filesBefore = _factory.Drive.Files.Count;

        Assert.Equal(HttpStatusCode.NoContent,
            (await client.DeleteAsync($"/api/documents/{kept.Id}?propertyId={property.Id}")).StatusCode);
        Assert.Equal(filesBefore, _factory.Drive.Files.Count);

        Assert.Equal(HttpStatusCode.NoContent,
            (await client.DeleteAsync($"/api/documents/{purged.Id}?propertyId={property.Id}&deleteFromDrive=true")).StatusCode);
        Assert.Equal(filesBefore - 1, _factory.Drive.Files.Count);

        // Both are gone from the app either way.
        var remaining = await (await client.GetAsync($"/api/properties/{property.Id}/documents"))
            .Content.ReadFromJsonAsync<List<DocumentDto>>();
        Assert.Empty(remaining!);
    }

    [Fact]
    public async Task Disconnecting_ReturnsToBlob_AndLeavesExistingDocumentsOpenable()
    {
        var client = await CreateAuthenticatedClientAsync();
        var property = await TestData.CreatePropertyAsync(client);
        await ConnectDriveAsync(client, property.Id);
        var document = (await (await client.PostAsync("/api/documents/upload", DriveUpload(property.Id)))
            .Content.ReadFromJsonAsync<DocumentDto>())!;
        var filesBefore = _factory.Drive.Files.Count;

        var disconnect = await client.DeleteAsync($"/api/drive/connection?propertyId={property.Id}");
        Assert.Equal(HttpStatusCode.NoContent, disconnect.StatusCode);

        var updated = await GetPropertyAsync(client, property.Id);
        Assert.Equal(DocumentStorageKind.Blob, updated.DocumentStorage);
        Assert.Null(updated.DriveFolderUrl);

        // The folder and its files are untouched — they're in someone's own Drive.
        Assert.Equal(filesBefore, _factory.Drive.Files.Count);

        // And the document uploaded while connected still opens.
        var url = await (await client.GetAsync($"/api/documents/{document.Id}/download-url?propertyId={property.Id}"))
            .Content.ReadFromJsonAsync<DownloadUrlResponse>();
        Assert.Equal(document.DriveWebViewLink, url!.DownloadUrl);
    }

    [Fact]
    public async Task ATamperedState_IsRejectedWithoutConnectingAnything()
    {
        var client = await CreateAuthenticatedClientAsync();
        var property = await TestData.CreatePropertyAsync(client);

        var callback = await client.GetAsync("/api/drive/callback?code=fake-code&state=not-a-real-state");

        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
        Assert.Contains("drive=failed", callback.Headers.Location!.ToString());
        Assert.Equal(DocumentStorageKind.Blob, (await GetPropertyAsync(client, property.Id)).DocumentStorage);
    }

    [Fact]
    public async Task WithoutARefreshToken_TheConnectionIsRefusedRatherThanHalfMade()
    {
        // An access token alone works for an hour and then fails mysteriously — better to fail now.
        var client = await CreateAuthenticatedClientAsync();
        var property = await TestData.CreatePropertyAsync(client);
        _factory.Drive.RefreshTokenToIssue = null;

        var callback = await ConnectDriveAsync(client, property.Id);

        Assert.Contains("drive=failed", callback.Headers.Location!.ToString());
        Assert.Equal(DocumentStorageKind.Blob, (await GetPropertyAsync(client, property.Id)).DocumentStorage);
    }

    [Fact]
    public async Task ARevokedGrant_ReportsThatTheConnectionNeedsRenewing()
    {
        var client = await CreateAuthenticatedClientAsync();
        var property = await TestData.CreatePropertyAsync(client);
        await ConnectDriveAsync(client, property.Id);

        _factory.Drive.ConnectionExpired = true;
        var upload = await client.PostAsync("/api/documents/upload", DriveUpload(property.Id));

        // 409 with a code the frontend keys off, not a 500 — nothing is broken, the grant just needs
        // remaking.
        Assert.Equal(HttpStatusCode.Conflict, upload.StatusCode);
        Assert.Contains("drive_connection_expired", await upload.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task TheRefreshToken_NeverLeavesTheServer()
    {
        var client = await CreateAuthenticatedClientAsync();
        var property = await TestData.CreatePropertyAsync(client);
        await ConnectDriveAsync(client, property.Id);

        var propertyJson = await (await client.GetAsync($"/api/properties/{property.Id}")).Content.ReadAsStringAsync();
        var usersJson = await (await client.GetAsync("/api/users")).Content.ReadAsStringAsync();
        var meJson = await (await client.GetAsync("/api/auth/me")).Content.ReadAsStringAsync();

        foreach (var json in new[] { propertyJson, usersJson, meJson })
        {
            Assert.DoesNotContain("fake-refresh-token", json);
            Assert.DoesNotContain("RefreshToken", json, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void TheDefaultStorageKind_IsBlob()
    {
        // Load-bearing: EF Cosmos stores this enum as an integer and every document written before
        // Drive existed has no such property, which deserializes to 0. Blob being 0 is what makes
        // the whole feature migration-free, so reordering the enum would silently relabel them.
        Assert.Equal(DocumentStorageKind.Blob, default(DocumentStorageKind));
        Assert.Equal(0, (int)DocumentStorageKind.Blob);
    }
}
