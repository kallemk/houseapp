using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;

namespace HouseApp.Api.Services;

/// <summary>
/// Issues SAS URLs for direct client-to-Blob upload/download so large files never pass through the API.
/// Supports two auth modes transparently: shared-key (local dev via Azurite connection string, where
/// <see cref="BlobClient.CanGenerateSasUri"/> is true) and Entra ID / user-delegation SAS (production,
/// where the BlobServiceClient is authenticated via managed identity and has no account key).
/// </summary>
public class BlobStorageService(BlobServiceClient blobServiceClient) : IBlobStorageService
{
    private const string ContainerName = "documents";
    private static readonly TimeSpan SasLifetime = TimeSpan.FromMinutes(15);

    public async Task<(string UploadUrl, string BlobPath)> GetUploadUrlAsync(string propertyId, string fileName, string contentType)
    {
        var blobPath = $"{propertyId}/{Guid.NewGuid()}-{fileName}";
        var containerClient = blobServiceClient.GetBlobContainerClient(ContainerName);
        var blobClient = containerClient.GetBlobClient(blobPath);

        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = ContainerName,
            BlobName = blobPath,
            Resource = "b",
            ExpiresOn = DateTimeOffset.UtcNow.Add(SasLifetime),
            ContentType = contentType,
        };
        sasBuilder.SetPermissions(BlobSasPermissions.Create | BlobSasPermissions.Write);

        var sasUri = await BuildSasUriAsync(blobClient, sasBuilder);
        return (sasUri.ToString(), blobPath);
    }

    public async Task<string> GetDownloadUrlAsync(string blobPath)
    {
        var containerClient = blobServiceClient.GetBlobContainerClient(ContainerName);
        var blobClient = containerClient.GetBlobClient(blobPath);

        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = ContainerName,
            BlobName = blobPath,
            Resource = "b",
            ExpiresOn = DateTimeOffset.UtcNow.Add(SasLifetime),
        };
        sasBuilder.SetPermissions(BlobSasPermissions.Read);

        var sasUri = await BuildSasUriAsync(blobClient, sasBuilder);
        return sasUri.ToString();
    }

    public Task DeleteAsync(string blobPath)
    {
        var containerClient = blobServiceClient.GetBlobContainerClient(ContainerName);
        return containerClient.DeleteBlobIfExistsAsync(blobPath);
    }

    private async Task<Uri> BuildSasUriAsync(BlobClient blobClient, BlobSasBuilder sasBuilder)
    {
        if (blobClient.CanGenerateSasUri)
        {
            // Local dev (Azurite connection string) — signed locally with the shared key, no network call.
            return blobClient.GenerateSasUri(sasBuilder);
        }

        // Production (managed identity) — no account key exists, so request a short-lived user delegation key.
        var delegationKeyOptions = new BlobGetUserDelegationKeyOptions(DateTimeOffset.UtcNow.Add(SasLifetime))
        {
            StartsOn = DateTimeOffset.UtcNow.AddMinutes(-5),
        };
        var delegationKey = await blobServiceClient.GetUserDelegationKeyAsync(delegationKeyOptions);

        var sasQuery = sasBuilder.ToSasQueryParameters(delegationKey.Value, blobServiceClient.AccountName);
        var builder = new UriBuilder(blobClient.Uri) { Query = sasQuery.ToString() };
        return builder.Uri;
    }
}
