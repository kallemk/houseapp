namespace HouseApp.Api.Models;

public class Document
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    // Partition key — groups all documents for a property together.
    public required string PropertyId { get; set; }

    public string? RenovationEntryId { get; set; }
    public required string FileName { get; set; }
    public required string ContentType { get; set; }
    public required string BlobPath { get; set; }
    public long SizeBytes { get; set; }
    public DocumentCategory Category { get; set; }
    public required string UploadedByUserId { get; set; }
    public DateTimeOffset UploadedAt { get; set; } = DateTimeOffset.UtcNow;
}
