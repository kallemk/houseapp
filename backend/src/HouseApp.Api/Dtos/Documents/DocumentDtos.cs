using System.Text.Json.Serialization;
using HouseApp.Api.Models;

namespace HouseApp.Api.Dtos.Documents;

public record DocumentDto(
    string Id,
    string PropertyId,
    string? ProjectId,
    DateOnly Date,
    /// <summary>Null on documents uploaded before titles existed — fall back to FileName.</summary>
    string? Title,
    string FileName,
    string ContentType,
    long SizeBytes,
    DocumentCategory Category,
    DocumentStorageKind StorageKind,
    /// <summary>Drive's own "open this file" link. Null on Blob documents, which go through a SAS URL instead.</summary>
    string? DriveWebViewLink,
    string UploadedByUserId,
    DateTimeOffset UploadedAt);

/// <summary>
/// Step 1 of upload. The reply tells the client *how* to upload, because which backend a property
/// uses is the server's business — the client shouldn't have to look at the property to find out,
/// and a client working from a stale cache shouldn't be able to pick the wrong one.
/// </summary>
public record UploadUrlRequest(string PropertyId, string FileName, string ContentType);

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum UploadMode
{
    /// <summary>PUT the bytes straight to Blob Storage using the SAS URL, then POST the metadata.</summary>
    Sas,

    /// <summary>POST the file to /api/documents/upload — Drive can't take a direct browser upload.</summary>
    Drive,
}

public record UploadUrlResponse(UploadMode Mode, string? UploadUrl, string? BlobPath);

/// <summary>Step 2 of a Blob upload: after the client PUTs the file, save its metadata.</summary>
public record CreateDocumentRequest(
    string PropertyId,
    string? ProjectId,
    DateOnly Date,
    string? Title,
    string FileName,
    string ContentType,
    string BlobPath,
    long SizeBytes,
    DocumentCategory Category);

/// <summary>
/// The parts of a document that are the app's own record of it, rather than the file: what it's
/// called here, what kind of thing it is, and what date it represents. Editable after upload.
///
/// Deliberately not FileName, ContentType or SizeBytes — those describe the stored file and would
/// start lying the moment they were edited.
/// </summary>
public record UpdateDocumentRequest(string? Title, DateOnly Date, DocumentCategory Category);

public record DownloadUrlResponse(string DownloadUrl);

/// <summary>Null detaches the document from whatever project it was on.</summary>
public record SetDocumentProjectRequest(string? ProjectId);
