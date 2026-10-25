using System.Text;
using HouseApp.Api.Services;

namespace HouseApp.Api.Tests;

public class FakeBlobStorageService : IBlobStorageService
{
    /// <summary>Paths that behave as if the blob had been deleted behind the app's back.</summary>
    public HashSet<string> MissingPaths { get; } = [];

    public static byte[] ContentFor(string blobPath) => Encoding.UTF8.GetBytes($"content of {blobPath}");

    public Task<(string UploadUrl, string BlobPath)> GetUploadUrlAsync(string propertyId, string fileName, string contentType)
    {
        var blobPath = $"{propertyId}/{Guid.NewGuid()}-{fileName}";
        return Task.FromResult(($"https://fake.blob.local/{blobPath}?sas=fake", blobPath));
    }

    public Task<string> GetDownloadUrlAsync(string blobPath) =>
        Task.FromResult($"https://fake.blob.local/{blobPath}?sas=fake");

    public Task DeleteAsync(string blobPath) => Task.CompletedTask;

    public Task<Stream> OpenReadAsync(string blobPath) =>
        MissingPaths.Contains(blobPath)
            ? throw new FileNotFoundException($"No blob at {blobPath}")
            : Task.FromResult<Stream>(new MemoryStream(ContentFor(blobPath)));
}
