using SmartNest.DeviceService.Dtos;
using SmartNest.DeviceService.Repositories;
using SmartNest.Shared.Security;

namespace SmartNest.DeviceService.Handlers;

/// <summary>
/// Handles <c>GET /devices/{id}</c>. Any authenticated role may read; caller must own the
/// device's home (verified against the Cosmos-level <c>homes</c> document's OwnerId, not
/// the JWT's self-asserted <c>homeId</c> claim - mirrors Home Service's ownership pattern).
/// </summary>
public sealed class GetDeviceHandler
{
    private readonly IDeviceRepository _repository;
    private readonly IHomeOwnershipRepository _homeOwnershipRepository;

    public GetDeviceHandler(IDeviceRepository repository, IHomeOwnershipRepository homeOwnershipRepository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _homeOwnershipRepository = homeOwnershipRepository ?? throw new ArgumentNullException(nameof(homeOwnershipRepository));
    }

    public async Task<DeviceResponse> HandleAsync(
        CurrentUser user,
        string deviceId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (string.IsNullOrWhiteSpace(deviceId))
            throw new ArgumentException("DeviceId is required.", nameof(deviceId));

        AuthorizationGuard.RequireRole(user, "SmartNest.Owner", "SmartNest.Technician", "SmartNest.Guest");

        var device = await _repository.GetAsync(deviceId, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Device '{deviceId}' was not found.");

        var ownerId = await _homeOwnershipRepository.GetOwnerIdAsync(device.HomeId, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Home '{device.HomeId}' was not found.");
        AuthorizationGuard.RequireOwnership(user, ownerId);

        return DeviceResponse.FromDomain(device);
    }
}
