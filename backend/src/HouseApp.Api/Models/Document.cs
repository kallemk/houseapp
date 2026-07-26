namespace HouseApp.Api.Models;

public class Document
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    // Partition key — groups all documents for a property together.
    public required string PropertyId { get; set; }

    public string? RenovationEntryId { get; set; }

    // The date this document represents/is dated (e.g. a receipt's purchase date), for timeline
    // placement — distinct from UploadedAt, which is when the file was actually uploaded.
    public DateOnly Date { get; set; }

    public required string FileName { get; set; }
    public required string ContentType { get; set; }
    public required string BlobPath { get; set; }
    public long SizeBytes { get; set; }
    public DocumentCategory Category { get; set; }
    public required string UploadedByUserId { get; set; }
    public DateTimeOffset UploadedAt { get; set; } = DateTimeOffset.UtcNow;
}
