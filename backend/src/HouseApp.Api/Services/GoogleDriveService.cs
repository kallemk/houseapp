using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Google;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;

namespace HouseApp.Api.Services;

/// <summary>
/// Talks to Google Drive on behalf of whoever connected the property.
///
/// The token endpoints are plain HTTP rather than the SDK's authorization-code flow machinery: that
/// machinery wants to own token storage, and this app already has somewhere to put it (encrypted on
/// the user document). Two form posts are less code than bending it into shape. Drive calls do use
/// the SDK, via a credential built from the access token we just fetched.
/// </summary>
public class GoogleDriveService(
    IConfiguration configuration,
    IHttpClientFactory httpClientFactory,
    ILogger<GoogleDriveService> logger) : IGoogleDriveService
{
    private const string TokenEndpoint = "https://oauth2.googleapis.com/token";
    private const string AuthorizationEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";

    /// <summary>
    /// Non-sensitive per Google's classification: basic verification, no security assessment. It only
    /// reaches files this app created, which covers the whole lifecycle because the app creates the
    /// folder and every upload goes through it. The broader "drive" scope is restricted and would
    /// need an assessment — see docs/google-drive-integration.md.
    /// </summary>
    private const string Scope = "https://www.googleapis.com/auth/drive.file";

    private const string FolderMimeType = "application/vnd.google-apps.folder";

    private string? ClientId => configuration["Authentication:Google:ClientId"];
    private string? ClientSecret => configuration["Authentication:Google:ClientSecret"];
    private string? RedirectUri => configuration["Authentication:Google:DriveRedirectUri"];

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ClientId)
        && !string.IsNullOrWhiteSpace(ClientSecret)
        && !string.IsNullOrWhiteSpace(RedirectUri);

    public string BuildAuthorizationUrl(string state)
    {
        var query = new Dictionary<string, string?>
        {
            ["client_id"] = ClientId,
            ["redirect_uri"] = RedirectUri,
            ["response_type"] = "code",
            ["scope"] = Scope,
            // Both are required to get a refresh token at all: offline asks for one, and consent
            // forces Google to re-issue it. Without prompt=consent a user who has already granted
            // access gets an access token and no refresh token, and the connection dies in an hour.
            ["access_type"] = "offline",
            ["prompt"] = "consent",
            ["include_granted_scopes"] = "true",
            ["state"] = state,
        };

        var encoded = string.Join('&', query.Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value ?? string.Empty)}"));
        return $"{AuthorizationEndpoint}?{encoded}";
    }

    public async Task<DriveTokens> ExchangeCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var response = await PostTokenRequestAsync(new Dictionary<string, string>
        {
            ["code"] = code,
            ["grant_type"] = "authorization_code",
            ["redirect_uri"] = RedirectUri!,
        }, cancellationToken);

        return new DriveTokens(response.AccessToken, response.RefreshToken);
    }

    public async Task<string> GetAccessTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var response = await PostTokenRequestAsync(new Dictionary<string, string>
        {
            ["refresh_token"] = refreshToken,
            ["grant_type"] = "refresh_token",
        }, cancellationToken);

        return response.AccessToken;
    }

    public async Task<DriveFolder> CreateFolderAsync(
        string accessToken,
        string name,
        CancellationToken cancellationToken = default)
    {
        using var drive = CreateDriveService(accessToken);

        var request = drive.Files.Create(new Google.Apis.Drive.v3.Data.File
        {
            Name = name,
            MimeType = FolderMimeType,
        });
        request.Fields = "id,webViewLink";

        var created = await ExecuteAsync(() => request.ExecuteAsync(cancellationToken));
        return new DriveFolder(created.Id, created.WebViewLink);
    }

    public async Task<DriveUploadResult> UploadAsync(
        string accessToken,
        string folderId,
        string fileName,
        string contentType,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        using var drive = CreateDriveService(accessToken);

        var request = drive.Files.Create(
            new Google.Apis.Drive.v3.Data.File
            {
                Name = fileName,
                Parents = [folderId],
            },
            content,
            contentType);
        request.Fields = "id,webViewLink";

        var upload = await ExecuteAsync(() => request.UploadAsync(cancellationToken));
        if (upload.Status != Google.Apis.Upload.UploadStatus.Completed)
        {
            throw new InvalidOperationException("Upload to Google Drive did not complete.", upload.Exception);
        }

        var file = request.ResponseBody;
        return new DriveUploadResult(file.Id, file.WebViewLink);
    }

    public async Task DeleteFileAsync(
        string accessToken,
        string fileId,
        CancellationToken cancellationToken = default)
    {
        using var drive = CreateDriveService(accessToken);

        try
        {
            await ExecuteAsync(() => drive.Files.Delete(fileId).ExecuteAsync(cancellationToken));
        }
        catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.NotFound)
        {
            // Already gone — someone deleted it in Drive directly. Removing the app's record of a
            // file that no longer exists is exactly what the caller wants, so this isn't an error.
            logger.LogInformation("Drive file {FileId} was already gone.", fileId);
        }
    }

    private DriveService CreateDriveService(string accessToken) =>
        new(new BaseClientService.Initializer
        {
            HttpClientInitializer = GoogleCredential.FromAccessToken(accessToken),
            ApplicationName = "HusTracker",
        });

    private async Task<TokenResponse> PostTokenRequestAsync(
        Dictionary<string, string> parameters,
        CancellationToken cancellationToken)
    {
        parameters["client_id"] = ClientId!;
        parameters["client_secret"] = ClientSecret!;

        var client = httpClientFactory.CreateClient(nameof(GoogleDriveService));
        var response = await client.PostAsync(TokenEndpoint, new FormUrlEncodedContent(parameters), cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            // Deliberately not logging the body at Error with the full payload — it echoes back
            // request parameters. The status and Google's error code are enough to diagnose.
            logger.LogWarning("Google token endpoint returned {Status}.", response.StatusCode);

            // invalid_grant is Google's answer for a revoked, expired or already-used token. That's
            // a connection the user has to remake, not a transient failure worth retrying.
            if (body.Contains("invalid_grant", StringComparison.OrdinalIgnoreCase))
            {
                throw new DriveConnectionExpiredException("The Google Drive connection is no longer valid.");
            }

            throw new InvalidOperationException($"Google token request failed with {response.StatusCode}.");
        }

        var tokens = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken);
        return tokens ?? throw new InvalidOperationException("Google token response could not be read.");
    }

    /// <summary>Turns Drive's 401/403 into the "reconnect" signal the UI knows how to explain.</summary>
    private static async Task<T> ExecuteAsync<T>(Func<Task<T>> call)
    {
        try
        {
            return await call();
        }
        catch (GoogleApiException ex) when (
            ex.HttpStatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw new DriveConnectionExpiredException("Google Drive rejected the stored credentials.", ex);
        }
    }

    private record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("refresh_token")] string? RefreshToken);
}
