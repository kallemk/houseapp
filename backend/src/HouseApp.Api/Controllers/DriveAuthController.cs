using HouseApp.Api.Data;
using HouseApp.Api.Dtos.Documents;
using HouseApp.Api.Extensions;
using HouseApp.Api.Models;
using HouseApp.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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
    IDriveAccessTokenResolver driveTokens,
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

            // Renewing an expired grant, or reconnecting after a disconnect, must land in the folder
            // the documents are already in. drive.file lets the same Google account reach what the
            // app made before, so the old tree is reused whenever this new grant can still see it.
            var reusable = property.GoogleDriveFolderId is { } previousRoot
                && await drive.IsFolderUsableAsync(tokens.AccessToken, previousRoot, cancellationToken);

            if (!reusable)
            {
                await CreateFolderTreeAsync(property, tokens.AccessToken, cancellationToken);
            }

            user.GoogleDriveRefreshTokenProtected = protector.ProtectRefreshToken(tokens.RefreshToken);
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
    /// A fresh root with "Allmänt" and "Projekt" inside, for a first connection or when the previous
    /// tree can't be reached with the new grant (another Google account, or trashed in Drive).
    /// </summary>
    private async Task CreateFolderTreeAsync(
        Property property,
        string accessToken,
        CancellationToken cancellationToken)
    {
        var folder = await drive.CreateFolderAsync(
            accessToken,
            $"HusTracker – {property.Nickname}",
            parentFolderId: null,
            cancellationToken);

        // Made up front so the folder looks organised the moment you open it in Drive, rather
        // than growing a structure as files happen to arrive. DriveFolderResolver still creates
        // them on demand, for properties connected before this existed.
        var general = await drive.CreateFolderAsync(
            accessToken, DriveFolderResolver.GeneralFolderName, folder.Id, cancellationToken);
        var projects = await drive.CreateFolderAsync(
            accessToken, DriveFolderResolver.ProjectsFolderName, folder.Id, cancellationToken);

        property.GoogleDriveFolderId = folder.Id;
        property.GoogleDriveFolderUrl = folder.WebViewLink;
        property.GoogleDriveGeneralFolderId = general.Id;
        property.GoogleDriveProjectsFolderId = projects.Id;

        // Any project folders point into the tree being replaced. Left alone, new documents would be
        // filed into the old structure, outside the new root, where nobody would think to look.
        foreach (var project in await db.Projects.Where(p => p.PropertyId == property.Id).ToListAsync(cancellationToken))
        {
            project.GoogleDriveFolderId = null;
        }
    }

    /// <summary>
    /// Whether the property's Drive connection still works, so the UI can say "renew" *before*
    /// someone tries to upload. Costs one token refresh against Google and nothing in Cosmos.
    ///
    /// Only a rejected grant reports Expired. Google being briefly unreachable reports Ok — the upload
    /// will say so if it persists, and a false alarm would send people through consent for nothing.
    /// </summary>
    [HttpGet("status")]
    public async Task<IActionResult> Status([FromQuery] string propertyId, CancellationToken cancellationToken)
    {
        if (!await db.CanAccessPropertyAsync(propertyId, User.CurrentUserId()))
        {
            return NotFound();
        }

        var property = await db.Properties.FindAsync([propertyId], cancellationToken);
        if (property is null)
        {
            return NotFound();
        }

        if (!property.UsesGoogleDrive)
        {
            return Ok(new DriveStatusResponse(DriveConnectionState.NotConnected));
        }

        try
        {
            await driveTokens.GetForPropertyAsync(property, cancellationToken);
            return Ok(new DriveStatusResponse(DriveConnectionState.Ok));
        }
        catch (DriveConnectionExpiredException)
        {
            return Ok(new DriveStatusResponse(DriveConnectionState.Expired));
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException)
        {
            logger.LogWarning(ex, "Could not check the Google Drive connection for {PropertyId}.", propertyId);
            return Ok(new DriveStatusResponse(DriveConnectionState.Ok));
        }
    }

    /// <summary>
    /// Stops new uploads going to Drive. **Never touches the Drive folders or the files in them** —
    /// they're in someone's own Drive, and the app's job here is to stop pointing at them, not to tidy
    /// up.
    ///
    /// The folder ids are deliberately *kept*: clearing ConnectedByUserId is enough to send uploads
    /// back to Blob (see Property.UsesGoogleDrive), and keeping the ids is what lets a later reconnect
    /// with the same Google account carry on in the same folder instead of starting a second one.
    /// Documents already uploaded keep their DriveFileId and webViewLink, so they still open.
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
