using System.Collections.Concurrent;
using System.Text;
using HouseApp.Api.Services;

namespace HouseApp.Api.Tests;

public class FakeBlobStorageService : IBlobStorageService
{
    /// <summary>Paths that behave as if the blob had been deleted behind the app's back.</summary>
    public HashSet<string> MissingPaths { get; } = [];

    /// <summary>What GetSizeAsync reports for a path. Anything not listed is a small file.</summary>
    public ConcurrentDictionary<string, long> Sizes { get; } = new();

    public const long DefaultSize = 1024;

    public ConcurrentBag<string> Deleted { get; } = [];

    public static byte[] ContentFor(string blobPath) => Encoding.UTF8.GetBytes($"content of {blobPath}");

    public Task<(string UploadUrl, string BlobPath)> GetUploadUrlAsync(string propertyId, string fileName, string contentType)
    {
        var blobPath = $"{propertyId}/{Guid.NewGuid()}-{fileName}";
        return Task.FromResult(($"https://fake.blob.local/{blobPath}?sas=fake", blobPath));
    }

    public Task<string> GetDownloadUrlAsync(string blobPath) =>
        Task.FromResult($"https://fake.blob.local/{blobPath}?sas=fake");

    public Task DeleteAsync(string blobPath)
    {
        Deleted.Add(blobPath);
        return Task.CompletedTask;
    }

    public Task<long?> GetSizeAsync(string blobPath) =>
        Task.FromResult<long?>(MissingPaths.Contains(blobPath) ? null : Sizes.GetValueOrDefault(blobPath, DefaultSize));

    public Task<Stream> OpenReadAsync(string blobPath) =>
        MissingPaths.Contains(blobPath)
            ? throw new FileNotFoundException($"No blob at {blobPath}")
            : Task.FromResult<Stream>(new MemoryStream(ContentFor(blobPath)));
}
