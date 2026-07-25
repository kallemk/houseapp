using HouseApp.Api.Services;

namespace HouseApp.Api.Tests;

public class FakeBlobStorageService : IBlobStorageService
{
    public Task<(string UploadUrl, string BlobPath)> GetUploadUrlAsync(string propertyId, string fileName, string contentType)
    {
        var blobPath = $"{propertyId}/{Guid.NewGuid()}-{fileName}";
        return Task.FromResult(($"https://fake.blob.local/{blobPath}?sas=fake", blobPath));
    }

    public Task<string> GetDownloadUrlAsync(string blobPath) =>
        Task.FromResult($"https://fake.blob.local/{blobPath}?sas=fake");

    public Task DeleteAsync(string blobPath) => Task.CompletedTask;
}
