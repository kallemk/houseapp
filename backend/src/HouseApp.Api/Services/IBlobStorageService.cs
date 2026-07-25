namespace HouseApp.Api.Services;

public interface IBlobStorageService
{
    /// <summary>Issues a short-lived, write-only SAS URL the client can PUT the file to directly.</summary>
    Task<(string UploadUrl, string BlobPath)> GetUploadUrlAsync(string propertyId, string fileName, string contentType);

    /// <summary>Issues a short-lived, read-only SAS URL for downloading a previously uploaded blob.</summary>
    Task<string> GetDownloadUrlAsync(string blobPath);

    Task DeleteAsync(string blobPath);
}
