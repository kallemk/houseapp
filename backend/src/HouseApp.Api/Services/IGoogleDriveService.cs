namespace HouseApp.Api.Services;

/// <summary>What the OAuth code exchange gives back.</summary>
/// <param name="RefreshToken">
/// Null when Google declines to issue one — it only does so with access_type=offline and
/// prompt=consent, which is why the connect endpoint always sends both.
/// </param>
public record DriveTokens(string AccessToken, string? RefreshToken);

public record DriveFolder(string Id, string WebViewLink);

public record DriveUploadResult(string FileId, string WebViewLink);

/// <summary>
/// The Google Drive side of document storage. An interface for the same reason
/// <see cref="IGoogleTokenValidator"/> is one: so the tests can substitute a fake and exercise the
/// whole document path without talking to Google.
///
/// Deliberately *not* a shared abstraction with <see cref="IBlobStorageService"/>. The two backends
/// work differently in a way an interface would have to paper over — Blob hands the browser a SAS
/// URL and never sees the bytes, Drive can't, so the file goes through the API. Forcing one shape
/// would mean methods that throw on one implementation.
/// </summary>
public interface IGoogleDriveService
{
    /// <summary>True when a client id and secret are configured — the Drive flow returns 503 without them.</summary>
    bool IsConfigured { get; }

    /// <summary>The Google consent URL to send the browser to.</summary>
    string BuildAuthorizationUrl(string state);

    Task<DriveTokens> ExchangeCodeAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>Trades the stored refresh token for a short-lived access token. Every call below needs one.</summary>
    Task<string> GetAccessTokenAsync(string refreshToken, CancellationToken cancellationToken = default);

    Task<DriveFolder> CreateFolderAsync(string accessToken, string name, CancellationToken cancellationToken = default);

    Task<DriveUploadResult> UploadAsync(
        string accessToken,
        string folderId,
        string fileName,
        string contentType,
        Stream content,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves the file to the owner's Drive trash. Tolerates a file that's already gone — the user can
    /// delete it in Drive at any time, and that must not block removing the app's own record of it.
    /// </summary>
    Task DeleteFileAsync(string accessToken, string fileId, CancellationToken cancellationToken = default);
}

/// <summary>Thrown when Drive rejects the stored refresh token — the connection needs remaking.</summary>
public class DriveConnectionExpiredException(string message, Exception? inner = null)
    : Exception(message, inner);
