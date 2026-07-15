using SmartNest.DeviceService.Dtos;
using SmartNest.DeviceService.Repositories;
using SmartNest.Shared.Security;

namespace SmartNest.DeviceService.Handlers;

/// <summary>
/// Handles <c>GET /devices/{id}</c>. Any authenticated role (Owner/Technician/Guest) may
/// read; caller's <c>homeId</c> claim must match the device's home.
/// </summary>
public sealed class GetDeviceHandler
{
    private readonly IDeviceRepository _repository;

    public GetDeviceHandler(IDeviceRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<DeviceResponse> HandleAsync(
        CurrentUser user,
        string deviceId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (string.IsNullOrWhiteSpace(deviceId))
            throw new ArgumentException("DeviceId is required.", nameof(deviceId));

        var device = await _repository.GetAsync(deviceId, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Device '{deviceId}' was not found.");

        AuthorizationGuard.RequireHomeIdMatch(user, device.HomeId);

        return DeviceResponse.FromDomain(device);
    }
}
