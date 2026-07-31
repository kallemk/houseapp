using System.Security.Claims;
using HouseApp.Api.Data;
using HouseApp.Api.Dtos.Documents;
using HouseApp.Api.Extensions;
using HouseApp.Api.Models;
using HouseApp.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HouseApp.Api.Controllers;

[ApiController]
[Authorize]
public class DocumentsController(
    AppDbContext db,
    IBlobStorageService blobStorage,
    IGoogleDriveService drive,
    IDriveAccessTokenResolver driveTokens,
    IDriveFolderResolver driveFolders) : ControllerBase
{
    /// <summary>
    /// Cap on files routed through the API, which only happens for Drive. Blob uploads go straight
    /// from the browser to storage and are unaffected. Chosen for the F1 App Service, where every
    /// byte through the API shares a 60 CPU-minute daily quota.
    /// </summary>
    private const long MaxDriveUploadBytes = 25 * 1024 * 1024;

    [HttpGet("api/properties/{propertyId}/documents")]
    public async Task<ActionResult<List<DocumentDto>>> GetForProperty(string propertyId)
    {
        if (!await db.CanAccessPropertyAsync(propertyId, User.CurrentUserId()))
        {
            return NotFound();
        }

        var documents = await db.Documents
            .Where(d => d.PropertyId == propertyId)
            .OrderByDescending(d => d.Date)
            .ToListAsync();
        return Ok(documents.Select(ToDto));
    }

    /// <summary>
    /// Attaches an already-uploaded document to a project, or detaches it when projectId is null.
    /// Separate from the project itself because documents live in their own container — a project is
    /// written whole, but its attachments aren't part of that document.
    /// </summary>
    [HttpPut("api/documents/{id}/project")]
    public async Task<IActionResult> SetProject(
        string id,
        [FromQuery] string propertyId,
        SetDocumentProjectRequest request,
        CancellationToken cancellationToken = default)
    {
        var property = await LoadAccessiblePropertyAsync(propertyId);
        if (property is null)
        {
            return NotFound();
        }

        var document = await db.Documents
            .Where(d => d.PropertyId == propertyId && d.Id == id)
            .FirstOrDefaultAsync(cancellationToken);
        if (document is null)
        {
            return NotFound();
        }

        // Re-file it in Drive to match: into the project's folder, or back to "Allmänt" when
        // detached. Done *before* the metadata is saved, so a Drive failure leaves both sides
        // consistent rather than recording an attachment whose file is filed somewhere else.
        if (document.StorageKind == DocumentStorageKind.Drive
            && property.UsesGoogleDrive
            && document.DriveFileId is { } fileId)
        {
            try
            {
                var accessToken = await driveTokens.GetForPropertyAsync(property, cancellationToken);
                var folderId = await driveFolders.GetUploadFolderIdAsync(
                    accessToken, property, request.ProjectId, cancellationToken);
                await drive.MoveFileAsync(accessToken, fileId, folderId, cancellationToken);
            }
            catch (DriveConnectionExpiredException)
            {
                return DriveConnectionGone();
            }
        }

        document.ProjectId = request.ProjectId;
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Says how to upload to this property. On Blob that's a SAS URL to PUT to; on Drive there's no
    /// equivalent (it would mean handing the browser a Drive token), so the answer is "post the file
    /// to the API instead".
    /// </summary>
    [HttpPost("api/documents/upload-url")]
    public async Task<ActionResult<UploadUrlResponse>> GetUploadUrl(UploadUrlRequest request)
    {
        var property = await LoadAccessiblePropertyAsync(request.PropertyId);
        if (property is null)
        {
            return NotFound();
        }

        if (property.UsesGoogleDrive)
        {
            return Ok(new UploadUrlResponse(UploadMode.Drive, UploadUrl: null, BlobPath: null));
        }

        var (uploadUrl, blobPath) = await blobStorage.GetUploadUrlAsync(request.PropertyId, request.FileName, request.ContentType);
        return Ok(new UploadUrlResponse(UploadMode.Sas, uploadUrl, blobPath));
    }

    /// <summary>Saves the metadata for a file the client has just PUT to Blob Storage.</summary>
    [HttpPost("api/documents")]
    public async Task<ActionResult<DocumentDto>> Create(CreateDocumentRequest request)
    {
        var property = await LoadAccessiblePropertyAsync(request.PropertyId);
        if (property is null)
        {
            return NotFound();
        }

        // A client working from a stale cache could otherwise write a Blob-shaped row against a
        // property that has since moved to Drive, pointing at a blob nobody ever uploaded.
        if (property.UsesGoogleDrive)
        {
            return Conflict(new { message = "This property stores documents in Google Drive — use /api/documents/upload." });
        }

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return BadRequest(new { message = "A title is required." });
        }

        var document = new Document
        {
            PropertyId = request.PropertyId,
            ProjectId = request.ProjectId,
            Date = request.Date,
            Title = request.Title.Trim(),
            FileName = request.FileName,
            ContentType = request.ContentType,
            StorageKind = DocumentStorageKind.Blob,
            BlobPath = request.BlobPath,
            SizeBytes = request.SizeBytes,
            Category = request.Category,
            UploadedByUserId = CurrentUserId,
        };
        db.Documents.Add(document);
        await db.SaveChangesAsync();
        return Ok(ToDto(document));
    }

    /// <summary>
    /// The Drive upload path: the file itself comes through the API, which streams it into the
    /// property's Drive folder and writes the metadata row.
    ///
    /// This is the one place documents pass through the API — Blob's SAS design deliberately avoids
    /// it, and Drive leaves no choice without putting an access token in the browser.
    /// </summary>
    [HttpPost("api/documents/upload")]
    [RequestSizeLimit(MaxDriveUploadBytes)]
    public async Task<ActionResult<DocumentDto>> Upload(
        [FromForm] DriveUploadForm form,
        CancellationToken cancellationToken)
    {
        var property = await LoadAccessiblePropertyAsync(form.PropertyId);
        if (property is null)
        {
            return NotFound();
        }

        if (!property.UsesGoogleDrive)
        {
            return Conflict(new { message = "This property stores documents in Blob Storage — use /api/documents/upload-url." });
        }

        if (string.IsNullOrWhiteSpace(form.Title))
        {
            return BadRequest(new { message = "A title is required." });
        }

        if (form.File is null || form.File.Length == 0)
        {
            return BadRequest(new { message = "No file was uploaded." });
        }

        var contentType = string.IsNullOrWhiteSpace(form.File.ContentType)
            ? "application/octet-stream"
            : form.File.ContentType;

        try
        {
            var accessToken = await driveTokens.GetForPropertyAsync(property, cancellationToken);
            // "Allmänt", or the project's own folder under "Projekt" — created on the way if needed.
            var folderId = await driveFolders.GetUploadFolderIdAsync(
                accessToken, property, form.ProjectId, cancellationToken);

            await using var stream = form.File.OpenReadStream();
            var uploaded = await drive.UploadAsync(
                accessToken,
                folderId,
                form.File.FileName,
                contentType,
                stream,
                cancellationToken);

            var document = new Document
            {
                PropertyId = form.PropertyId,
                ProjectId = form.ProjectId,
                Date = form.Date,
                Title = form.Title.Trim(),
                FileName = form.File.FileName,
                ContentType = contentType,
                StorageKind = DocumentStorageKind.Drive,
                DriveFileId = uploaded.FileId,
                DriveWebViewLink = uploaded.WebViewLink,
                SizeBytes = form.File.Length,
                Category = form.Category,
                UploadedByUserId = CurrentUserId,
            };
            db.Documents.Add(document);
            await db.SaveChangesAsync(cancellationToken);
            return Ok(ToDto(document));
        }
        catch (DriveConnectionExpiredException)
        {
            return DriveConnectionGone();
        }
    }

    /// <summary>
    /// Corrects the app's record of a document — its title, category and date.
    ///
    /// Metadata only: nothing here touches the stored file, so it behaves identically on Blob and
    /// Drive. In particular the title is the app's label and is **not** the Drive file's name, which
    /// stays as the filename it was uploaded under.
    /// </summary>
    [HttpPut("api/documents/{id}")]
    public async Task<ActionResult<DocumentDto>> Update(
        string id,
        [FromQuery] string propertyId,
        UpdateDocumentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!await db.CanAccessPropertyAsync(propertyId, User.CurrentUserId()))
        {
            return NotFound();
        }

        var document = await db.Documents
            .Where(d => d.PropertyId == propertyId && d.Id == id)
            .FirstOrDefaultAsync(cancellationToken);
        if (document is null)
        {
            return NotFound();
        }

        // Same rule as upload. Documents predating titles have none, so editing one is where that
        // finally gets filled in rather than an exception to the rule.
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return BadRequest(new { message = "A title is required." });
        }

        document.Title = request.Title.Trim();
        document.Date = request.Date;
        document.Category = request.Category;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(ToDto(document));
    }

    /// <summary>
    /// A URL to open the document with. Blob documents get a short-lived SAS URL; Drive documents get
    /// the link stored at upload, so opening one needs neither a Drive call nor a live connection.
    /// </summary>
    [HttpGet("api/documents/{id}/download-url")]
    public async Task<ActionResult<DownloadUrlResponse>> GetDownloadUrl(string id, [FromQuery] string propertyId)
    {
        if (!await db.CanAccessPropertyAsync(propertyId, User.CurrentUserId()))
        {
            return NotFound();
        }

        var document = await db.Documents
            .Where(d => d.PropertyId == propertyId && d.Id == id)
            .FirstOrDefaultAsync();
        if (document is null)
        {
            return NotFound();
        }

        if (document.StorageKind == DocumentStorageKind.Drive)
        {
            return document.DriveWebViewLink is { } link
                ? Ok(new DownloadUrlResponse(link))
                : NotFound();
        }

        var url = await blobStorage.GetDownloadUrlAsync(document.BlobPath!);
        return Ok(new DownloadUrlResponse(url));
    }

    /// <summary>
    /// Removes the app's record of the document, and its file.
    ///
    /// For Blob that's unconditional — the blob is ours and nothing else can reach it. For Drive it's
    /// opt-in via <paramref name="deleteFromDrive"/>: the file sits in someone's personal Drive, and
    /// deleting from there is their call to make each time, not a side effect of tidying up the app.
    /// </summary>
    [HttpDelete("api/documents/{id}")]
    public async Task<IActionResult> Delete(
        string id,
        [FromQuery] string propertyId,
        [FromQuery] bool deleteFromDrive = false,
        CancellationToken cancellationToken = default)
    {
        var property = await LoadAccessiblePropertyAsync(propertyId);
        if (property is null)
        {
            return NotFound();
        }

        var document = await db.Documents
            .Where(d => d.PropertyId == propertyId && d.Id == id)
            .FirstOrDefaultAsync(cancellationToken);
        if (document is null)
        {
            return NotFound();
        }

        if (document.StorageKind == DocumentStorageKind.Drive)
        {
            if (deleteFromDrive && document.DriveFileId is { } fileId)
            {
                try
                {
                    var accessToken = await driveTokens.GetForPropertyAsync(property, cancellationToken);
                    await drive.DeleteFileAsync(accessToken, fileId, cancellationToken);
                }
                catch (DriveConnectionExpiredException)
                {
                    // Refuse rather than silently dropping the row: the caller explicitly asked for
                    // the file to go, and leaving it while forgetting where it is would be worse.
                    return DriveConnectionGone();
                }
            }
        }
        else if (document.BlobPath is { } blobPath)
        {
            await blobStorage.DeleteAsync(blobPath);
        }

        db.Documents.Remove(document);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    private async Task<Property?> LoadAccessiblePropertyAsync(string propertyId)
    {
        if (!await db.CanAccessPropertyAsync(propertyId, User.CurrentUserId()))
        {
            return null;
        }

        return await db.Properties.FindAsync(propertyId);
    }

    /// <summary>
    /// 409 rather than 500: nothing is broken, the Drive grant just needs remaking. The frontend
    /// keys off this to show "Drive-anslutningen behöver förnyas" instead of a generic failure.
    /// </summary>
    private ObjectResult DriveConnectionGone() =>
        Conflict(new { message = "The Google Drive connection needs to be renewed.", code = "drive_connection_expired" });

    private static DocumentDto ToDto(Document d) =>
        new(d.Id, d.PropertyId, d.ProjectId, d.Date, d.Title, d.FileName, d.ContentType, d.SizeBytes,
            d.Category, d.StorageKind, d.DriveWebViewLink, d.UploadedByUserId, d.UploadedAt);
}

/// <summary>
/// Multipart form for a Drive upload. A class with [FromForm] rather than a record, because model
/// binding needs settable properties and an IFormFile can't ride in a JSON body.
/// </summary>
public class DriveUploadForm
{
    public string PropertyId { get; set; } = string.Empty;
    public string? ProjectId { get; set; }
    public DateOnly Date { get; set; }
    public string? Title { get; set; }
    public DocumentCategory Category { get; set; }
    public IFormFile? File { get; set; }
}
