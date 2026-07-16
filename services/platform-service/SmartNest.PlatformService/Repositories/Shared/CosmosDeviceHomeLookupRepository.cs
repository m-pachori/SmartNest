using Microsoft.Azure.Cosmos;
using Newtonsoft.Json;

namespace SmartNest.PlatformService.Repositories.Shared;

/// <summary>
/// Cosmos DB-backed <see cref="IDeviceHomeLookupRepository"/>. Reads the <c>devices</c>
/// container (partition key <c>/homeId</c>) by a cross-partition query on <c>id</c>, since
/// the caller only has the device id. Point-read only; never writes to this container -
/// Device Service remains the sole owner of that data.
/// </summary>
internal sealed class CosmosDeviceHomeLookupRepository : IDeviceHomeLookupRepository
{
    private readonly Container _devicesContainer;

    public CosmosDeviceHomeLookupRepository(Container devicesContainer)
    {
        _devicesContainer = devicesContainer ?? throw new ArgumentNullException(nameof(devicesContainer));
    }

    public async Task<string?> GetHomeIdAsync(string deviceId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
            throw new ArgumentException("DeviceId is required.", nameof(deviceId));

        var query = new QueryDefinition("SELECT VALUE c.homeId FROM c WHERE c.id = @deviceId")
            .WithParameter("@deviceId", deviceId);

        using var iterator = _devicesContainer.GetItemQueryIterator<string>(query);
        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
            var homeId = page.FirstOrDefault();
            if (homeId is not null)
                return homeId;
        }

        return null;
    }
}
