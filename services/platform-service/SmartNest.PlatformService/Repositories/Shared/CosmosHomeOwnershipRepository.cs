using System.Net;
using Microsoft.Azure.Cosmos;
using Newtonsoft.Json;

namespace SmartNest.PlatformService.Repositories.Shared;

/// <summary>
/// Cosmos DB-backed <see cref="IHomeOwnershipRepository"/>. Reads the <c>homes</c>
/// container (partition key <c>/homeId</c>, document id == homeId). Point-read only;
/// never writes to this container - Home Service remains the sole owner of that data.
/// </summary>
internal sealed class CosmosHomeOwnershipRepository : IHomeOwnershipRepository
{
    private readonly Container _homesContainer;

    public CosmosHomeOwnershipRepository(Container homesContainer)
    {
        _homesContainer = homesContainer ?? throw new ArgumentNullException(nameof(homesContainer));
    }

    public async Task<string?> GetOwnerIdAsync(string homeId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(homeId))
            throw new ArgumentException("HomeId is required.", nameof(homeId));

        try
        {
            var response = await _homesContainer
                .ReadItemAsync<HomeOwnerDocument>(homeId, new PartitionKey(homeId), cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            return response.Resource.OwnerId;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    private sealed class HomeOwnerDocument
    {
        [JsonProperty("ownerId")]
        public string OwnerId { get; set; } = default!;
    }
}
