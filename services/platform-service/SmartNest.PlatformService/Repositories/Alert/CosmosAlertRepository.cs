using Microsoft.Azure.Cosmos;
using SmartNest.PlatformService.Persistence.Alert;
using SmartNest.Shared.Persistence;

namespace SmartNest.PlatformService.Repositories.Alert;

/// <summary>
/// Cosmos DB-backed <see cref="IAlertRepository"/>. Container: <c>alerts</c>, partition
/// key: <c>/homeId</c> (already provisioned in Task 1).
/// </summary>
internal sealed class CosmosAlertRepository : CosmosRepositoryBase<AlertDocument>, IAlertRepository
{
    public CosmosAlertRepository(Container container) : base(container)
    {
    }

    public async Task<Domain.Alert.Alert?> GetAsync(string homeId, string alertId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(homeId))
            throw new ArgumentException("HomeId is required.", nameof(homeId));
        if (string.IsNullOrWhiteSpace(alertId))
            throw new ArgumentException("AlertId is required.", nameof(alertId));

        var document = await GetAsync(alertId, partitionKeyValue: homeId, cancellationToken).ConfigureAwait(false);
        return document?.ToDomain();
    }

    public async Task CreateAsync(Domain.Alert.Alert alert, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(alert);

        await CreateAsync(alert.ToDocument(), partitionKeyValue: alert.HomeId, cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAsync(Domain.Alert.Alert alert, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(alert);

        await UpsertAsync(alert.ToDocument(), partitionKeyValue: alert.HomeId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Domain.Alert.Alert>> GetByHomeIdAsync(string homeId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(homeId))
            throw new ArgumentException("HomeId is required.", nameof(homeId));

        var query = new QueryDefinition("SELECT * FROM c WHERE c.homeId = @homeId ORDER BY c.createdAt DESC")
            .WithParameter("@homeId", homeId);

        var results = new List<Domain.Alert.Alert>();
        using var iterator = Container.GetItemQueryIterator<AlertDocument>(query, requestOptions: new QueryRequestOptions
        {
            PartitionKey = new PartitionKey(homeId),
        });

        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
            results.AddRange(page.Select(d => d.ToDomain()));
        }

        return results;
    }
}
