using Microsoft.Azure.Cosmos;
using SmartNest.DeviceService.Domain;
using SmartNest.DeviceService.Persistence;
using SmartNest.Shared.Persistence;

namespace SmartNest.DeviceService.Repositories;

/// <summary>
/// Cosmos DB-backed <see cref="IDeviceRepository"/>. Container: <c>devices</c>,
/// partition key: <c>/homeId</c> (see smartnest-plan.md Task 3).
/// </summary>
internal sealed class CosmosDeviceRepository : CosmosRepositoryBase<DeviceDocument>, IDeviceRepository
{
    public CosmosDeviceRepository(Container container) : base(container)
    {
    }

    public async Task<Device?> GetAsync(string deviceId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
            throw new ArgumentException("DeviceId is required.", nameof(deviceId));

        // Cross-partition query: the flat /devices/{id} routes don't carry homeId, so we
        // can't do a direct point-read (which requires the partition key up-front).
        var query = new QueryDefinition("SELECT * FROM c WHERE c.id = @id").WithParameter("@id", deviceId);
        using var iterator = Container.GetItemQueryIterator<DeviceDocument>(query);

        if (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);

            // DeviceId is a generated GUID (see Device.Register), so more than one match
            // for the same id should never happen. Guard against it explicitly rather than
            // silently returning an arbitrary match, which could otherwise leak a device
            // from a different home if a duplicate id was ever introduced (e.g. bad data
            // import/migration).
            if (response.Count > 1)
            {
                throw new InvalidOperationException(
                    $"Data integrity error: found {response.Count} devices with id '{deviceId}' across different homes.");
            }

            var document = response.FirstOrDefault();
            if (document is not null)
                return document.ToDomain();
        }

        return null;
    }

    public async Task CreateAsync(Device device, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(device);

        await CreateAsync(device.ToDocument(), partitionKeyValue: device.HomeId, cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAsync(Device device, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(device);

        await UpsertAsync(device.ToDocument(), partitionKeyValue: device.HomeId, cancellationToken).ConfigureAwait(false);
    }

    /// <remarks>
    /// The <c>new</c> modifier is intentional: this signature coincidentally matches the
    /// inherited <c>CosmosRepositoryBase&lt;T&gt;.DeleteAsync(id, partitionKeyValue, ct)</c>,
    /// so this member hides (rather than overrides) it - see <see cref="IDeviceRepository"/>.
    /// </remarks>
    public new async Task<bool> DeleteAsync(string deviceId, string homeId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
            throw new ArgumentException("DeviceId is required.", nameof(deviceId));
        if (string.IsNullOrWhiteSpace(homeId))
            throw new ArgumentException("HomeId is required.", nameof(homeId));

        // Explicitly qualified: this method's (string, string, CancellationToken) shape
        // coincidentally matches the inherited CosmosRepositoryBase<T>.DeleteAsync(id,
        // partitionKeyValue, ct) signature, so it hides rather than overrides it - call
        // the base implementation explicitly to avoid relying on overload-hiding rules.
        return await base.DeleteAsync(deviceId, partitionKeyValue: homeId, cancellationToken).ConfigureAwait(false);
    }
}
