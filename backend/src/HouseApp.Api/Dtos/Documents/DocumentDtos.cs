using HouseApp.Api.Models;

namespace HouseApp.Api.Dtos.Documents;

public record DocumentDto(
    string Id,
    string PropertyId,
    string? ProjectId,
    DateOnly Date,
    string FileName,
    string ContentType,
    long SizeBytes,
    DocumentCategory Category,
    string UploadedByUserId,
    DateTimeOffset UploadedAt);

/// <summary>Step 1 of upload: ask the API for a place to PUT the file directly to Blob Storage.</summary>
public record UploadUrlRequest(string PropertyId, string FileName, string ContentType);

public record UploadUrlResponse(string UploadUrl, string BlobPath);

/// <summary>Step 2 of upload: after the client PUTs the file to Blob Storage, save its metadata.</summary>
public record CreateDocumentRequest(
    string PropertyId,
    string? ProjectId,
    DateOnly Date,
    string FileName,
    string ContentType,
    string BlobPath,
    long SizeBytes,
    DocumentCategory Category);

public record DownloadUrlResponse(string DownloadUrl);

/// <summary>Null detaches the document from whatever project it was on.</summary>
public record SetDocumentProjectRequest(string? ProjectId);
