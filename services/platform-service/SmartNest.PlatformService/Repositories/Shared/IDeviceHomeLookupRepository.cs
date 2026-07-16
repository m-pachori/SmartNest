namespace SmartNest.PlatformService.Repositories.Shared;

/// <summary>
/// Read-only lookup against the (shared) <c>devices</c> Cosmos container - used by the
/// Media bounded context to resolve a device's <c>homeId</c> (needed to partition
/// <c>media-metadata</c> documents by <c>/homeId</c>) from just a device id parsed out of
/// a blob path. Cross-partition query since the caller only knows the device id, not its
/// home id (the <c>devices</c> container's partition key).
/// </summary>
public interface IDeviceHomeLookupRepository
{
    /// <summary>Returns the device's HomeId, or null if the device doesn't exist.</summary>
    Task<string?> GetHomeIdAsync(string deviceId, CancellationToken cancellationToken = default);
}
