using System.Net;
using Microsoft.Azure.Cosmos;

namespace SmartNest.Shared.Persistence;

/// <summary>
/// Thin generic base over the Cosmos DB SDK's <see cref="Container"/> providing the
/// common get/create/upsert/delete-by-id-and-partition-key operations every service's
/// repository needs, so each service doesn't re-implement Cosmos SDK boilerplate.
/// </summary>
public abstract class CosmosRepositoryBase<T>
    where T : class
{
    protected Container Container { get; }

    protected CosmosRepositoryBase(Container container)
    {
        Container = container ?? throw new ArgumentNullException(nameof(container));
    }

    public virtual async Task<T?> GetAsync(
        string id,
        string partitionKeyValue,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await Container
                .ReadItemAsync<T>(id, new PartitionKey(partitionKeyValue), cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public virtual async Task<T> CreateAsync(
        T item,
        string partitionKeyValue,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);

        var response = await Container
            .CreateItemAsync(item, new PartitionKey(partitionKeyValue), cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return response.Resource;
    }

    public virtual async Task<T> UpsertAsync(
        T item,
        string partitionKeyValue,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);

        var response = await Container
            .UpsertItemAsync(item, new PartitionKey(partitionKeyValue), cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return response.Resource;
    }

    public virtual async Task<bool> DeleteAsync(
        string id,
        string partitionKeyValue,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await Container
                .DeleteItemAsync<T>(id, new PartitionKey(partitionKeyValue), cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            return true;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }
}
