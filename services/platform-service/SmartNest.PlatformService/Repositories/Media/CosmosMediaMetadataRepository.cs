using Microsoft.Azure.Cosmos;
using SmartNest.PlatformService.Persistence.Media;
using SmartNest.Shared.Persistence;

namespace SmartNest.PlatformService.Repositories.Media;

/// <summary>
/// Cosmos DB-backed <see cref="IMediaMetadataRepository"/>. Container:
/// <c>media-metadata</c>, partition key: <c>/homeId</c> (already provisioned in Task 1).
/// </summary>
internal sealed class CosmosMediaMetadataRepository : CosmosRepositoryBase<MediaMetadataDocument>, IMediaMetadataRepository
{
    public CosmosMediaMetadataRepository(Container container) : base(container)
    {
    }

    public async Task<bool> ExistsByBlobNameAsync(string homeId, string blobName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(homeId))
            throw new ArgumentException("HomeId is required.", nameof(homeId));
        if (string.IsNullOrWhiteSpace(blobName))
            throw new ArgumentException("BlobName is required.", nameof(blobName));

        var query = new QueryDefinition("SELECT VALUE COUNT(1) FROM c WHERE c.homeId = @homeId AND c.blobName = @blobName")
            .WithParameter("@homeId", homeId)
            .WithParameter("@blobName", blobName);

        using var iterator = Container.GetItemQueryIterator<int>(query, requestOptions: new QueryRequestOptions
        {
            PartitionKey = new PartitionKey(homeId),
        });

        if (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
            return page.FirstOrDefault() > 0;
        }

        return false;
    }

    public Task CreateAsync(MediaMetadataDocument document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        return CreateAsync(document, partitionKeyValue: document.HomeId, cancellationToken);
    }
}
