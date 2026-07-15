using SmartNest.DeviceService.Domain;

namespace SmartNest.DeviceService.Repositories;

public interface IDeviceRepository
{
    /// <summary>
    /// Cross-partition lookup by deviceId alone. The flat routes (<c>GET/PATCH/DELETE
    /// /devices/{id}</c>) don't carry the <c>homeId</c> partition key, so this cannot be a
    /// direct point-read - see <c>CosmosDeviceRepository</c> for the query implementation.
    /// </summary>
    Task<Device?> GetAsync(string deviceId, CancellationToken cancellationToken = default);

    Task CreateAsync(Device device, CancellationToken cancellationToken = default);

    Task UpdateAsync(Device device, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(string deviceId, string homeId, CancellationToken cancellationToken = default);
}
