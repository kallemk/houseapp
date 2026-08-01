namespace HouseApp.Api.Models;

public class ApplicationUser
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public required string Email { get; set; }

    // Nullable: users added via the admin page for Google-only access never get one. Existing
    // documents all have a value, so relaxing this is safe (removing/loosening a field is fine;
    // it's *adding* a required one that breaks tolerant reads — see CLAUDE.md).
    public string? PasswordHash { get; set; }

    // Set opportunistically on first Google sign-in. Nullable because documents predating this
    // field have no such JSON property and deserialize it as null.
    public string? GoogleSubjectId { get; set; }

    public required string DisplayName { get; set; }

    // Regular user is the default for everyone; admins additionally manage users and renovation
    // types. Non-nullable bool is deliberate — unlike Property.MemberUserIds, "false" is exactly
    // the right reading of a document that predates this field. DbSeeder handles the resulting
    // bootstrap problem (a database where nobody is an admin yet, so nobody can promote anyone).
    public bool IsAdmin { get; set; }

    /// <summary>
    /// Refuses this account at sign-in and kills any session it already holds.
    ///
    /// This exists because the `users` container stopped being the sign-in allowlist. Google sign-in
    /// now creates an account for anyone who doesn't have one, so *deleting* a user no longer revokes
    /// anything — they'd simply get a fresh account on their next sign-in. Blocking is what revocation
    /// means now, and deleting is only for tidying up.
    ///
    /// A plain non-nullable bool: accounts written before this field existed have no such JSON
    /// property, which reads as false, and "not blocked" is the right reading of every one of them.
    /// </summary>
    public bool IsBlocked { get; set; }

    /// <summary>
    /// Google Drive OAuth refresh token, encrypted with the app's Data Protection key ring before it
    /// gets here — see <c>DriveTokenProtector</c>. Never leaves the server: no DTO exposes it, and
    /// nothing reads it except the code exchanging it for a short-lived access token.
    ///
    /// It's on the user rather than the property because the grant belongs to a Google account, not
    /// a house — one person connecting three properties gets one token. Note the coupling this adds
    /// to the key ring: losing it already means everyone signs in again, and now also means
    /// reconnecting Drive.
    /// </summary>
    public string? GoogleDriveRefreshTokenProtected { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
