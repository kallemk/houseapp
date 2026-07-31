using HouseApp.Api.Data;
using HouseApp.Api.Extensions;
using HouseApp.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HouseApp.Api.Controllers;

/// <summary>
/// Connecting a property's document storage to someone's Google Drive.
///
/// This is a real redirect-based OAuth flow, unlike sign-in — which is an ID-token flow with no
/// redirect and no client secret (see AuthController). Drive needs a long-lived grant to act on the
/// user's behalf, and there is no shortcut for that.
///
/// It stays compatible with the SameSite=Lax session cookie because /connect is reached by
/// **top-level browser navigation**, which Lax permits, and the callback returns to the same public
/// origin the app is served from. A callback pointing at the App Service hostname instead of the
/// Static Web App front door would land the user on the wrong origin without their cookie — the same
/// trap that ruled out a redirect flow for sign-in.
/// </summary>
[ApiController]
[Route("api/drive")]
[Authorize]
public class DriveAuthController(
    AppDbContext db,
    IGoogleDriveService drive,
    IDriveTokenProtector protector,
    IConfiguration configuration,
    ILogger<DriveAuthController> logger) : ControllerBase
{
    /// <summary>
    /// Starts the consent flow. Returns a 302 to Google rather than JSON, because the browser
    /// navigates here directly.
    /// </summary>
    [HttpGet("connect")]
    public async Task<IActionResult> Connect([FromQuery] string propertyId)
    {
        if (!drive.IsConfigured)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { message = "Google Drive is not configured on the server." });
        }

        var userId = User.CurrentUserId();
        // Strict membership, not demo access: binding your own Drive to the shared sandbox isn't a
        // sandbox action, and the grant being connected is a personal one.
        if (!await db.IsPropertyMemberAsync(propertyId, userId))
        {
            return NotFound();
        }

        var state = protector.ProtectState(new DriveOAuthState(propertyId, userId));
        return Redirect(drive.BuildAuthorizationUrl(state));
    }

    /// <summary>
    /// Where Google sends the browser back. Everything this needs comes out of the signed state, not
    /// the query string — the only thing taken on trust from Google is the authorization code, which
    /// is useless without the client secret.
    ///
    /// Deliberately [AllowAnonymous]: the session cookie does ride along (Lax permits it on a
    /// top-level GET), but the flow must not break if it didn't, and the state is the real proof.
    /// </summary>
    [HttpGet("callback")]
    [AllowAnonymous]
    public async Task<IActionResult> Callback(
        [FromQuery] string? code,
        [FromQuery] string? state,
        [FromQuery] string? error,
        CancellationToken cancellationToken)
    {
        // The user pressed "cancel" on Google's consent screen. Not an error worth a stack trace.
        if (!string.IsNullOrEmpty(error))
        {
            logger.LogInformation("Google Drive consent was declined: {Error}", error);
            return RedirectToApp(propertyId: null, "cancelled");
        }

        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
        {
            return RedirectToApp(propertyId: null, "failed");
        }

        if (protector.UnprotectState(state) is not { } verified)
        {
            // Forged, replayed or simply stale. Nothing here is trustworthy, so nothing is written.
            return RedirectToApp(propertyId: null, "failed");
        }

        // Re-checked rather than trusted from the state: membership can have changed while the user
        // was on Google's consent screen.
        if (!await db.IsPropertyMemberAsync(verified.PropertyId, verified.UserId))
        {
            return RedirectToApp(verified.PropertyId, "failed");
        }

        var property = await db.Properties.FindAsync([verified.PropertyId], cancellationToken);
        var user = await db.Users.FindAsync([verified.UserId], cancellationToken);
        if (property is null || user is null)
        {
            return RedirectToApp(verified.PropertyId, "failed");
        }

        try
        {
            var tokens = await drive.ExchangeCodeAsync(code, cancellationToken);
            if (tokens.RefreshToken is null)
            {
                // Shouldn't happen — the authorization URL always sends access_type=offline and
                // prompt=consent. If it does, an access token alone would work for an hour and then
                // fail mysteriously, so refuse the connection instead of half-making it.
                logger.LogWarning("Google returned no refresh token; refusing to half-connect Drive.");
                return RedirectToApp(verified.PropertyId, "failed");
            }

            var folder = await drive.CreateFolderAsync(
                tokens.AccessToken,
                $"HusTracker – {property.Nickname}",
                cancellationToken);

            user.GoogleDriveRefreshTokenProtected = protector.ProtectRefreshToken(tokens.RefreshToken);
            property.GoogleDriveFolderId = folder.Id;
            property.GoogleDriveFolderUrl = folder.WebViewLink;
            property.GoogleDriveConnectedByUserId = user.Id;
            await db.SaveChangesAsync(cancellationToken);

            return RedirectToApp(verified.PropertyId, "connected");
        }
        catch (Exception ex) when (ex is DriveConnectionExpiredException or InvalidOperationException)
        {
            logger.LogWarning(ex, "Failed to complete the Google Drive connection.");
            return RedirectToApp(verified.PropertyId, "failed");
        }
    }

    /// <summary>
    /// Forgets the connection. **Never touches the Drive folder or the files in it** — they're in
    /// someone's own Drive, and the app's job here is to stop pointing at them, not to tidy up.
    ///
    /// Documents already uploaded keep their DriveFileId and webViewLink, so they still open; new
    /// uploads go back to Blob Storage.
    /// </summary>
    [HttpDelete("connection")]
    public async Task<IActionResult> Disconnect([FromQuery] string propertyId)
    {
        var userId = User.CurrentUserId();
        if (!await db.IsPropertyMemberAsync(propertyId, userId))
        {
            return NotFound();
        }

        var property = await db.Properties.FindAsync(propertyId);
        if (property is null)
        {
            return NotFound();
        }

        property.GoogleDriveFolderId = null;
        property.GoogleDriveFolderUrl = null;
        property.GoogleDriveConnectedByUserId = null;
        // The user's refresh token is deliberately left alone: it may still be connecting another
        // property, and it's cheap to keep. Revoking access is done in the Google account settings.
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// Back into the SPA. Uses the configured public origin, since this runs at the end of a
    /// cross-site redirect chain where a relative path would resolve against Google's response.
    /// </summary>
    private IActionResult RedirectToApp(string? propertyId, string outcome)
    {
        var baseUrl = (configuration["Authentication:Google:DriveRedirectUri"] ?? string.Empty)
            .Replace("/api/drive/callback", string.Empty, StringComparison.OrdinalIgnoreCase);

        var path = propertyId is null
            ? "/properties"
            : $"/properties/{propertyId}/documents";

        return Redirect($"{baseUrl}{path}?drive={outcome}");
    }
}
