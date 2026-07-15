using Microsoft.Azure.Cosmos;
using SmartNest.HomeService.Domain;
using SmartNest.HomeService.Persistence;
using SmartNest.Shared.Persistence;

namespace SmartNest.HomeService.Repositories;

/// <summary>
/// Cosmos DB-backed <see cref="IHomeRepository"/>. Container: <c>homes</c>,
/// partition key: <c>/homeId</c> (see smartnest-plan.md Task 2).
/// </summary>
internal sealed class CosmosHomeRepository : CosmosRepositoryBase<HomeDocument>, IHomeRepository
{
    public CosmosHomeRepository(Container container) : base(container)
    {
    }

    public async Task<Home?> GetAsync(string homeId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(homeId))
            throw new ArgumentException("HomeId is required.", nameof(homeId));

        var document = await GetAsync(homeId, partitionKeyValue: homeId, cancellationToken).ConfigureAwait(false);
        return document?.ToDomain();
    }

    public async Task CreateAsync(Home home, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(home);

        await CreateAsync(home.ToDocument(), partitionKeyValue: home.HomeId, cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAsync(Home home, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(home);

        await UpsertAsync(home.ToDocument(), partitionKeyValue: home.HomeId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> DeleteAsync(string homeId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(homeId))
            throw new ArgumentException("HomeId is required.", nameof(homeId));

        return await DeleteAsync(homeId, partitionKeyValue: homeId, cancellationToken).ConfigureAwait(false);
    }
}
