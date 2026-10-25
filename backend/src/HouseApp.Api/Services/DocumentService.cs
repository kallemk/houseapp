using HouseApp.Api.Data;
using HouseApp.Api.Dtos.Documents;
using HouseApp.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace HouseApp.Api.Services;

/// <summary>
/// Changing a document's metadata and which project it belongs to, shared by the REST API
/// (<c>DocumentsController</c>) and the MCP tools. As with <see cref="ProjectService"/>, access is the
/// caller's job. Uploads stay in the controller: MCP never receives file bytes, it hands the user an
/// upload link instead.
/// </summary>
public class DocumentService(
    AppDbContext db,
    IGoogleDriveService drive,
    IDriveAccessTokenResolver driveTokens,
    IDriveFolderResolver driveFolders)
{
    public async Task<List<Document>> ListAsync(string propertyId) =>
        (await db.Documents.Where(d => d.PropertyId == propertyId).ToListAsync())
            .OrderByDescending(d => d.Date)
            .ToList();

    public async Task<Document?> FindAsync(string propertyId, string id, CancellationToken cancellationToken = default) =>
        await db.Documents
            .Where(d => d.PropertyId == propertyId && d.Id == id)
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>
    /// Metadata only — nothing here touches the stored file, so it behaves identically on Blob and
    /// Drive. In particular the title is the app's label and is **not** the Drive file's name, which
    /// stays as the filename it was uploaded under. The caller has checked the title isn't blank.
    /// </summary>
    public async Task UpdateMetadataAsync(
        Document document,
        string title,
        DateOnly date,
        DocumentCategory category,
        CancellationToken cancellationToken = default)
    {
        document.Title = title.Trim();
        document.Date = date;
        document.Category = category;
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Attaches the document to a project, or detaches it when <paramref name="projectId"/> is null.
    /// False when the project isn't one of this property's — an id from another household, or a
    /// stale one, must not become an attachment pointing nowhere.
    ///
    /// On Drive the file is re-filed to match: into the project's folder, or back to "Allmänt".
    /// That happens *before* the metadata is saved, so a Drive failure — surfacing as
    /// <see cref="DriveConnectionExpiredException"/> for the caller to explain — leaves both sides
    /// consistent rather than recording an attachment whose file is filed somewhere else.
    /// </summary>
    public async Task<bool> SetProjectAsync(
        Property property,
        Document document,
        string? projectId,
        CancellationToken cancellationToken = default)
    {
        if (projectId is not null)
        {
            var exists = (await db.Projects
                    .Where(p => p.PropertyId == property.Id && p.Id == projectId)
                    .Select(p => p.Id)
                    .ToListAsync(cancellationToken))
                .Count > 0;
            if (!exists)
            {
                return false;
            }
        }

        if (document.StorageKind == DocumentStorageKind.Drive
            && property.UsesGoogleDrive
            && document.DriveFileId is { } fileId)
        {
            var accessToken = await driveTokens.GetForPropertyAsync(property, cancellationToken);
            var folderId = await driveFolders.GetUploadFolderIdAsync(accessToken, property, projectId, cancellationToken);
            await drive.MoveFileAsync(accessToken, fileId, folderId, cancellationToken);
        }

        document.ProjectId = projectId;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public static DocumentDto ToDto(Document d) =>
        new(d.Id, d.PropertyId, d.ProjectId, d.Date, d.Title, d.FileName, d.ContentType, d.SizeBytes,
            d.Category, d.StorageKind, d.DriveWebViewLink, d.UploadedByUserId, d.UploadedAt);
}
