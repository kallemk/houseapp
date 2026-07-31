namespace HouseApp.Api.Models;

public class Document
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    // Partition key — groups all documents for a property together.
    public required string PropertyId { get; set; }

    // Mapped to the JSON property "RenovationEntryId" (see AppDbContext) — the documents container
    // isn't migrated, and the ProjectMigrator preserves entry ids as project ids, so the values
    // already stored still resolve.
    public string? ProjectId { get; set; }

    // The date this document represents/is dated (e.g. a receipt's purchase date), for timeline
    // placement — distinct from UploadedAt, which is when the file was actually uploaded.
    public DateOnly Date { get; set; }

    /// <summary>
    /// Human-readable label, because a filename like "scan_0042.pdf" says nothing. Optional and
    /// nullable — documents uploaded before this existed have none, and the UI falls back to
    /// FileName rather than showing a blank.
    /// </summary>
    public string? Title { get; set; }

    public required string FileName { get; set; }
    public required string ContentType { get; set; }

    /// <summary>
    /// Which backend holds the bytes. Absent on every document written before Drive existed, which
    /// reads as <see cref="DocumentStorageKind.Blob"/> — see the enum for why that ordering matters.
    /// </summary>
    public DocumentStorageKind StorageKind { get; set; }

    /// <summary>Set for <see cref="DocumentStorageKind.Blob"/> documents; null for Drive ones.</summary>
    public string? BlobPath { get; set; }

    /// <summary>Set for <see cref="DocumentStorageKind.Drive"/> documents; null for Blob ones.</summary>
    public string? DriveFileId { get; set; }

    /// <summary>
    /// Drive's own "open this file" URL, stored at upload rather than fetched on demand — opening a
    /// document shouldn't need a Drive round-trip, or an access token, or the connection to still be
    /// alive. It's the same link Drive shows in its UI, so Drive's sharing decides who can follow it.
    /// </summary>
    public string? DriveWebViewLink { get; set; }

    public long SizeBytes { get; set; }
    public DocumentCategory Category { get; set; }
    public required string UploadedByUserId { get; set; }
    public DateTimeOffset UploadedAt { get; set; } = DateTimeOffset.UtcNow;
}
