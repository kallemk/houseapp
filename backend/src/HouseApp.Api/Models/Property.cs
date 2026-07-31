namespace HouseApp.Api.Models;

public class Property
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public required string Nickname { get; set; }
    public required string Address { get; set; }

    // All nullable on purpose: properties created before these fields existed have no such JSON
    // property, which deserializes to the CLR default. A nullable Type shows as "—" in the UI
    // rather than silently claiming every pre-existing property is a House.
    public string? Address2 { get; set; }
    public string? PostalCode { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }

    /// <summary>Fastighetsbeteckning — the official Swedish property designation.</summary>
    public string? PropertyDesignation { get; set; }

    public int? YearBuilt { get; set; }
    public PropertyType? Type { get; set; }

    // WGS84, for the map on the dashboard. Stored rather than geocoded on every render: geocoding
    // is a third-party call that can fail, rate-limit or drift, and the answer never changes once
    // it's right. Both null => no map is shown.
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    public DateOnly PurchaseDate { get; set; }
    public decimal PurchasePrice { get; set; }

    // Nullable, not just an empty-by-default list: properties created before this field existed
    // have no such JSON property at all, which Cosmos deserializes as null rather than "[]" — EF
    // Core's "required collection" write-time check would otherwise reject that, and it's also
    // simply the honest type for data that predates the field. Always use PropertiesController's
    // null-safe IsMember() helper rather than calling .Contains() on this directly.
    //
    // Every account (there are only ever 2, admin-seeded) is connected automatically when a
    // property is created — see PropertiesController.Create. Filtered in application code, not
    // via a Cosmos query, to avoid array-Contains query translation entirely.
    public List<string>? MemberUserIds { get; set; } = [];

    /// <summary>
    /// The shared sandbox every signed-in user can see and edit, so someone with no property of
    /// their own has something to learn the app on. Deliberately a plain non-nullable bool: a
    /// missing JSON property reads as false, and "this existing property is not the demo" is exactly
    /// the right default.
    ///
    /// Only an admin can set it (PUT /api/properties/{id}/demo) — it's kept out of
    /// SavePropertyRequest so nobody can publish their own house to everyone by editing it. Deleting
    /// a property while this is set is refused.
    /// </summary>
    public bool IsDemo { get; set; }

    /// <summary>
    /// False = this property follows the central component registry; its effective component list is
    /// whatever PropertyComponents currently holds, and LocalComponents is not consulted. True = it
    /// has its own list, and only LocalComponents counts.
    ///
    /// This flag is the whole reason the feature is safe, and it must not be replaced by inferring
    /// the same thing from `LocalComponents is null or []`. Deleting every local component is a
    /// legitimate thing to do, and inferring "not customized" from an empty list would silently
    /// restore the central set on the next read — the same "can't tell 'never ran' from 'deliberately
    /// emptied'" mistake that made ProjectMigrator resurrect deleted projects.
    ///
    /// A plain non-nullable bool on purpose: properties that predate the field have no such JSON
    /// property, which reads as false, and "follows central" is exactly how they behaved before.
    /// </summary>
    public bool ComponentsCustomized { get; set; }

    /// <summary>
    /// This property's own component list, materialised from the central registry the first time
    /// anything here is edited. Nullable rather than "= []" for the usual reason (properties written
    /// before the field existed have no such JSON property) — read it as <c>?? []</c>, and only ever
    /// when <see cref="ComponentsCustomized"/> is true.
    /// </summary>
    public List<PropertyLocalComponent>? LocalComponents { get; set; }

    // --- Google Drive document storage -------------------------------------------------------
    //
    // All three null (the state of every property that predates this) means documents go to Blob
    // Storage. Set together when someone connects Drive, cleared together on disconnect.
    //
    // The folder is recorded here but the OAuth refresh token lives on the *user*
    // (ApplicationUser.GoogleDriveRefreshTokenProtected) — GoogleDriveConnectedByUserId says whose,
    // so one person can connect several properties from a single grant.
    //
    // One connection per property, not per member, and that is forced by the drive.file scope: it
    // only reaches files the granting user created through this app, so two members uploading under
    // their own tokens would each see half the folder. Everything therefore goes through the
    // connecting user's token; if they disconnect, uploads fail loudly rather than splitting.

    public string? GoogleDriveFolderId { get; set; }
    public string? GoogleDriveFolderUrl { get; set; }
    public string? GoogleDriveConnectedByUserId { get; set; }

    /// <summary>
    /// "Allmänt" and "Projekt" inside the property's folder — general documents go in the first,
    /// per-project subfolders live under the second.
    ///
    /// Nullable and created on demand rather than assumed: properties connected before this existed
    /// have a root folder and no subfolders at all, so both <see cref="Services.DriveFolderResolver"/>
    /// and anything else that needs them must be prepared to create them. Note that a null here means
    /// "not made yet", never "the user deleted it in Drive" — that case shows up as a Drive error.
    /// </summary>
    public string? GoogleDriveGeneralFolderId { get; set; }
    public string? GoogleDriveProjectsFolderId { get; set; }

    /// <summary>True when documents for this property go to Google Drive rather than Blob Storage.</summary>
    public bool UsesGoogleDrive => GoogleDriveFolderId is not null && GoogleDriveConnectedByUserId is not null;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
