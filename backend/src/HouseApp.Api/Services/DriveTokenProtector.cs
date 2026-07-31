using Microsoft.AspNetCore.DataProtection;

namespace HouseApp.Api.Services;

/// <summary>Who started a Drive connection, and for which property.</summary>
public record DriveOAuthState(string PropertyId, string UserId);

public interface IDriveTokenProtector
{
    string ProtectRefreshToken(string refreshToken);

    /// <summary>Null when the value can't be decrypted — a lost key ring, not a bug to throw on.</summary>
    string? UnprotectRefreshToken(string protectedRefreshToken);

    string ProtectState(DriveOAuthState state);

    /// <summary>Null when the state is tampered with, unreadable, or older than the allowed window.</summary>
    DriveOAuthState? UnprotectState(string protectedState);
}

/// <summary>
/// Encrypts the two things in this feature that must not be readable or forgeable: the stored refresh
/// token, and the OAuth <c>state</c> parameter.
///
/// Reuses the Data Protection key ring the auth cookie already depends on (see
/// <c>AddHouseAppDataProtection</c>) rather than inventing key management. Two separate purposes, so
/// a value from one context can never be replayed as the other.
///
/// The state is the CSRF defence for the whole flow: it's what proves the browser arriving at the
/// callback is the one that started at /connect, and it carries the property and user so the callback
/// doesn't have to trust anything in the query string. Time-limited because a state is only ever
/// seconds old in a real flow.
/// </summary>
public class DriveTokenProtector : IDriveTokenProtector
{
    private static readonly TimeSpan StateLifetime = TimeSpan.FromMinutes(15);

    private readonly IDataProtector _tokenProtector;
    private readonly ITimeLimitedDataProtector _stateProtector;
    private readonly ILogger<DriveTokenProtector> _logger;

    public DriveTokenProtector(IDataProtectionProvider provider, ILogger<DriveTokenProtector> logger)
    {
        _tokenProtector = provider.CreateProtector("HouseApp.GoogleDrive.RefreshToken");
        _stateProtector = provider.CreateProtector("HouseApp.GoogleDrive.OAuthState").ToTimeLimitedDataProtector();
        _logger = logger;
    }

    public string ProtectRefreshToken(string refreshToken) => _tokenProtector.Protect(refreshToken);

    public string? UnprotectRefreshToken(string protectedRefreshToken)
    {
        try
        {
            return _tokenProtector.Unprotect(protectedRefreshToken);
        }
        catch (Exception ex)
        {
            // The key ring is gone (App Service /home wiped, or the app renamed). Every stored token
            // is unreadable and every property has to be reconnected — worth a log, but the caller
            // just treats it as "not connected".
            _logger.LogWarning(ex, "A stored Google Drive refresh token could not be decrypted.");
            return null;
        }
    }

    public string ProtectState(DriveOAuthState state) =>
        _stateProtector.Protect($"{state.PropertyId}|{state.UserId}", StateLifetime);

    public DriveOAuthState? UnprotectState(string protectedState)
    {
        try
        {
            var parts = _stateProtector.Unprotect(protectedState).Split('|');
            return parts.Length == 2 ? new DriveOAuthState(parts[0], parts[1]) : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Rejected an invalid or expired Google Drive OAuth state.");
            return null;
        }
    }
}
