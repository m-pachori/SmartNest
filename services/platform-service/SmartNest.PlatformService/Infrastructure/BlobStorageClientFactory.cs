using Azure.Storage.Blobs;

namespace SmartNest.PlatformService.Infrastructure;

/// <summary>
/// Builds <see cref="BlobContainerClient"/>s for the Media bounded context (Task 9).
/// Wraps a <see cref="BlobServiceClient"/> constructed from the <c>AzureWebJobsStorage</c>
/// app setting - already a plain (non-Key-Vault-reference) value the Functions host
/// itself requires at startup (see infra/modules/function-app.bicep), so it's safe to
/// reuse here to reach the same shared platform storage account's blob containers.
/// </summary>
public interface IBlobStorageClientFactory
{
    BlobContainerClient GetContainerClient(string containerName);
}

public sealed class BlobStorageClientFactory : IBlobStorageClientFactory
{
    private readonly BlobServiceClient _blobServiceClient;

    public BlobStorageClientFactory(BlobServiceClient blobServiceClient)
    {
        _blobServiceClient = blobServiceClient ?? throw new ArgumentNullException(nameof(blobServiceClient));
    }

    public BlobContainerClient GetContainerClient(string containerName)
    {
        if (string.IsNullOrWhiteSpace(containerName))
            throw new ArgumentException("Container name is required.", nameof(containerName));

        return _blobServiceClient.GetBlobContainerClient(containerName);
    }
}
