using System.Collections.Concurrent;
using HouseApp.Api.Services;

namespace HouseApp.Api.Tests;

/// <summary>
/// Stands in for Google Drive, in the same spirit as <see cref="FakeGoogleTokenValidator"/>: only the
/// outbound calls are faked, so the endpoints, access checks, storage routing and metadata writes
/// under test are all the real ones.
///
/// It records the files it holds so tests can assert what actually reached (and left) "Drive", and it
/// can be told to reject the stored credentials so the reconnect path is testable.
/// </summary>
public class FakeGoogleDriveService : IGoogleDriveService
{
    /// <summary>Drive file id → the folder it went into. What survives is what wasn't deleted.</summary>
    public ConcurrentDictionary<string, string> Files { get; } = new();

    /// <summary>Folder id → (name, parent folder id). Enough to assert the whole tree's shape.</summary>
    public ConcurrentDictionary<string, (string Name, string? ParentId)> Folders { get; } = new();

    /// <summary>Folder ids whose parent is <paramref name="parentId"/>, by name.</summary>
    public string? FolderIdIn(string? parentId, string name) =>
        Folders.FirstOrDefault(f => f.Value.ParentId == parentId && f.Value.Name == name).Key;

    public string? FolderNameOf(string folderId) =>
        Folders.TryGetValue(folderId, out var folder) ? folder.Name : null;

    /// <summary>Flip to make every call behave like a revoked grant.</summary>
    public bool ConnectionExpired { get; set; }

    /// <summary>Set null to reproduce Google declining to issue a refresh token.</summary>
    public string? RefreshTokenToIssue { get; set; } = "fake-refresh-token";

    public bool IsConfigured { get; set; } = true;

    public string BuildAuthorizationUrl(string state) =>
        $"https://accounts.google.local/consent?state={Uri.EscapeDataString(state)}";

    public Task<DriveTokens> ExchangeCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        ThrowIfExpired();
        return Task.FromResult(new DriveTokens("fake-access-token", RefreshTokenToIssue));
    }

    public Task<string> GetAccessTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        ThrowIfExpired();
        return Task.FromResult("fake-access-token");
    }

    public Task<DriveFolder> CreateFolderAsync(
        string accessToken,
        string name,
        string? parentFolderId = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfExpired();
        var id = $"folder-{Guid.NewGuid()}";
        Folders[id] = (name, parentFolderId);
        return Task.FromResult(new DriveFolder(id, $"https://drive.local/folders/{id}"));
    }

    public Task<DriveUploadResult> UploadAsync(
        string accessToken,
        string folderId,
        string fileName,
        string contentType,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        ThrowIfExpired();
        var id = $"file-{Guid.NewGuid()}";
        Files[id] = folderId;
        return Task.FromResult(new DriveUploadResult(id, $"https://drive.local/file/{id}"));
    }

    public Task DeleteFileAsync(string accessToken, string fileId, CancellationToken cancellationToken = default)
    {
        ThrowIfExpired();
        Files.TryRemove(fileId, out _);
        return Task.CompletedTask;
    }

    private void ThrowIfExpired()
    {
        if (ConnectionExpired)
        {
            throw new DriveConnectionExpiredException("Fake Drive connection expired.");
        }
    }
}
