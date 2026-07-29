using System.Security.Claims;
using HouseApp.Api.Data;
using HouseApp.Api.Dtos.Documents;
using HouseApp.Api.Models;
using HouseApp.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HouseApp.Api.Controllers;

[ApiController]
[Authorize]
public class DocumentsController(AppDbContext db, IBlobStorageService blobStorage) : ControllerBase
{
    [HttpGet("api/properties/{propertyId}/documents")]
    public async Task<ActionResult<List<DocumentDto>>> GetForProperty(string propertyId)
    {
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
    public async Task<IActionResult> SetProject(string id, [FromQuery] string propertyId, SetDocumentProjectRequest request)
    {
        var document = await db.Documents
            .Where(d => d.PropertyId == propertyId && d.Id == id)
            .FirstOrDefaultAsync();
        if (document is null)
        {
            return NotFound();
        }

        document.ProjectId = request.ProjectId;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("api/documents/upload-url")]
    public async Task<ActionResult<UploadUrlResponse>> GetUploadUrl(UploadUrlRequest request)
    {
        var (uploadUrl, blobPath) = await blobStorage.GetUploadUrlAsync(request.PropertyId, request.FileName, request.ContentType);
        return Ok(new UploadUrlResponse(uploadUrl, blobPath));
    }

    [HttpPost("api/documents")]
    public async Task<ActionResult<DocumentDto>> Create(CreateDocumentRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var document = new Document
        {
            PropertyId = request.PropertyId,
            ProjectId = request.ProjectId,
            Date = request.Date,
            FileName = request.FileName,
            ContentType = request.ContentType,
            BlobPath = request.BlobPath,
            SizeBytes = request.SizeBytes,
            Category = request.Category,
            UploadedByUserId = userId,
        };
        db.Documents.Add(document);
        await db.SaveChangesAsync();
        return Ok(ToDto(document));
    }

    [HttpGet("api/documents/{id}/download-url")]
    public async Task<ActionResult<DownloadUrlResponse>> GetDownloadUrl(string id, [FromQuery] string propertyId)
    {
        var document = await db.Documents
            .Where(d => d.PropertyId == propertyId && d.Id == id)
            .FirstOrDefaultAsync();
        if (document is null)
        {
            return NotFound();
        }

        var url = await blobStorage.GetDownloadUrlAsync(document.BlobPath);
        return Ok(new DownloadUrlResponse(url));
    }

    [HttpDelete("api/documents/{id}")]
    public async Task<IActionResult> Delete(string id, [FromQuery] string propertyId)
    {
        var document = await db.Documents
            .Where(d => d.PropertyId == propertyId && d.Id == id)
            .FirstOrDefaultAsync();
        if (document is null)
        {
            return NotFound();
        }

        await blobStorage.DeleteAsync(document.BlobPath);
        db.Documents.Remove(document);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static DocumentDto ToDto(Document d) =>
        new(d.Id, d.PropertyId, d.ProjectId, d.Date, d.FileName, d.ContentType, d.SizeBytes, d.Category, d.UploadedByUserId, d.UploadedAt);
}
