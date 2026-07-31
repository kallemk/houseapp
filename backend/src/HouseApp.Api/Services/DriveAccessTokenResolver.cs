using HouseApp.Api.Data;
using HouseApp.Api.Models;

namespace HouseApp.Api.Services;

public interface IDriveAccessTokenResolver
{
    /// <summary>
    /// A usable access token for whoever connected this property.
    /// </summary>
    /// <exception cref="DriveConnectionExpiredException">
    /// The connecting user is gone, has no stored token, the token can't be decrypted, or Google has
    /// revoked it. All four mean the same thing to the user: reconnect.
    /// </exception>
    Task<string> GetForPropertyAsync(Property property, CancellationToken cancellationToken = default);
}

/// <summary>
/// The one place that turns "this property uses Drive" into "here is a token to call Drive with".
///
/// Every Drive operation goes through the *connecting* user's grant, not the caller's — see
/// Property.UsesGoogleDrive for why the drive.file scope forces that. So this deliberately ignores
/// who is making the request.
/// </summary>
public class DriveAccessTokenResolver(
    AppDbContext db,
    IGoogleDriveService drive,
    IDriveTokenProtector protector) : IDriveAccessTokenResolver
{
    public async Task<string> GetForPropertyAsync(Property property, CancellationToken cancellationToken = default)
    {
        if (property.GoogleDriveConnectedByUserId is not { } ownerId)
        {
            throw new DriveConnectionExpiredException("This property is not connected to Google Drive.");
        }

        var owner = await db.Users.FindAsync([ownerId], cancellationToken);
        if (owner?.GoogleDriveRefreshTokenProtected is not { } stored)
        {
            // The connecting user was deleted, or disconnected Drive from another property in a way
            // that cleared their token. The property still points at Drive but nothing can reach it.
            throw new DriveConnectionExpiredException("The person who connected Google Drive no longer has a valid connection.");
        }

        var refreshToken = protector.UnprotectRefreshToken(stored)
            ?? throw new DriveConnectionExpiredException("The stored Google Drive credentials could not be read.");

        return await drive.GetAccessTokenAsync(refreshToken, cancellationToken);
    }
}
