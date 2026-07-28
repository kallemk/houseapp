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

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
