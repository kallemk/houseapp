using HouseApp.Api.Data;
using HouseApp.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace HouseApp.Api.Services;

public interface IDriveFolderResolver
{
    /// <summary>
    /// The folder a document should land in, creating whatever is missing on the way.
    /// </summary>
    /// <param name="projectId">
    /// Null for a general document, which goes to "Allmänt". Otherwise the file goes to that
    /// project's own folder under "Projekt".
    /// </param>
    Task<string> GetUploadFolderIdAsync(
        string accessToken,
        Property property,
        string? projectId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Decides where in a property's Drive folder a document goes:
///
/// <code>
/// HusTracker – {property}
///   Allmänt                      ← documents not tied to a project
///   Projekt
///     {project name} {yyyy-MM-dd}  ← one per project, created on its first upload
/// </code>
///
/// Everything here is created lazily and idempotently, because the structure has to arrive on
/// properties that were connected before it existed and on projects that predate their first
/// document. Ids are saved as soon as a folder is made, *before* the upload that prompted it — a
/// failed upload must not leave an orphaned folder that gets recreated on the next attempt.
/// </summary>
public class DriveFolderResolver(AppDbContext db, IGoogleDriveService drive) : IDriveFolderResolver
{
    public const string GeneralFolderName = "Allmänt";
    public const string ProjectsFolderName = "Projekt";

    public async Task<string> GetUploadFolderIdAsync(
        string accessToken,
        Property property,
        string? projectId,
        CancellationToken cancellationToken = default)
    {
        if (property.GoogleDriveFolderId is not { } rootFolderId)
        {
            throw new DriveConnectionExpiredException("This property is not connected to Google Drive.");
        }

        if (projectId is null)
        {
            return await EnsureGeneralFolderAsync(accessToken, property, rootFolderId, cancellationToken);
        }

        // A project id that doesn't resolve (deleted, or belonging to another property) falls back to
        // the general folder rather than failing the upload — the file matters more than its filing.
        var project = (await db.Projects
                .Where(p => p.PropertyId == property.Id && p.Id == projectId)
                .ToListAsync(cancellationToken))
            .FirstOrDefault();

        if (project is null)
        {
            return await EnsureGeneralFolderAsync(accessToken, property, rootFolderId, cancellationToken);
        }

        if (project.GoogleDriveFolderId is { } existing)
        {
            return existing;
        }

        var projectsFolderId = await EnsureProjectsFolderAsync(accessToken, property, rootFolderId, cancellationToken);
        var folder = await drive.CreateFolderAsync(
            accessToken,
            FolderNameFor(project),
            projectsFolderId,
            cancellationToken);

        project.GoogleDriveFolderId = folder.Id;
        await db.SaveChangesAsync(cancellationToken);
        return folder.Id;
    }

    /// <summary>
    /// "{name} {created:yyyy-MM-dd}". The date is when the *project* was created, not when the folder
    /// was — so the name is stable and means something even if the first document arrives years later.
    /// Note the folder keeps this name if the project is later renamed; Drive is a filing cabinet, not
    /// a mirror of the database.
    /// </summary>
    public static string FolderNameFor(Project project) =>
        $"{project.Name} {project.CreatedAt:yyyy-MM-dd}";

    private async Task<string> EnsureGeneralFolderAsync(
        string accessToken,
        Property property,
        string rootFolderId,
        CancellationToken cancellationToken)
    {
        if (property.GoogleDriveGeneralFolderId is { } existing)
        {
            return existing;
        }

        var folder = await drive.CreateFolderAsync(accessToken, GeneralFolderName, rootFolderId, cancellationToken);
        property.GoogleDriveGeneralFolderId = folder.Id;
        await db.SaveChangesAsync(cancellationToken);
        return folder.Id;
    }

    private async Task<string> EnsureProjectsFolderAsync(
        string accessToken,
        Property property,
        string rootFolderId,
        CancellationToken cancellationToken)
    {
        if (property.GoogleDriveProjectsFolderId is { } existing)
        {
            return existing;
        }

        var folder = await drive.CreateFolderAsync(accessToken, ProjectsFolderName, rootFolderId, cancellationToken);
        property.GoogleDriveProjectsFolderId = folder.Id;
        await db.SaveChangesAsync(cancellationToken);
        return folder.Id;
    }
}
