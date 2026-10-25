namespace HouseApp.Api.Services;

public interface IBlobStorageService
{
    /// <summary>Issues a short-lived, write-only SAS URL the client can PUT the file to directly.</summary>
    Task<(string UploadUrl, string BlobPath)> GetUploadUrlAsync(string propertyId, string fileName, string contentType);

    /// <summary>Issues a short-lived, read-only SAS URL for downloading a previously uploaded blob.</summary>
    Task<string> GetDownloadUrlAsync(string blobPath);

    Task DeleteAsync(string blobPath);

    /// <summary>
    /// The stored size of a blob, or null if nothing was uploaded there. What the API records instead
    /// of the client's own claim, since a SAS upload has no size limit of its own.
    /// </summary>
    Task<long?> GetSizeAsync(string blobPath);

    /// <summary>
    /// Reads a blob's content through the API. Only the data export does this — everything else hands
    /// the browser a SAS URL instead. Throws if the blob doesn't exist.
    /// </summary>
    Task<Stream> OpenReadAsync(string blobPath);
}
