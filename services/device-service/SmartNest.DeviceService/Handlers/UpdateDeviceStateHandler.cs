using SmartNest.DeviceService.Dtos;
using SmartNest.DeviceService.Events;
using SmartNest.DeviceService.Repositories;
using SmartNest.DeviceService.Telemetry;
using SmartNest.Shared.Security;

namespace SmartNest.DeviceService.Handlers;

/// <summary>
/// Handles <c>PATCH /devices/{id}/state</c>. Owner/Technician may mutate; caller must own
/// the device's home (verified against the Cosmos-level <c>homes</c> document's OwnerId,
/// not the JWT's self-asserted <c>homeId</c> claim - mirrors Home Service's ownership
/// pattern). Increments the <c>device.state.changes</c> custom metric on every successful
/// update.
/// </summary>
public sealed class UpdateDeviceStateHandler
{
    private readonly IDeviceRepository _repository;
    private readonly IHomeOwnershipRepository _homeOwnershipRepository;
    private readonly DeviceEventPublisher _eventPublisher;

    public UpdateDeviceStateHandler(
        IDeviceRepository repository,
        IHomeOwnershipRepository homeOwnershipRepository,
        DeviceEventPublisher eventPublisher)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _homeOwnershipRepository = homeOwnershipRepository ?? throw new ArgumentNullException(nameof(homeOwnershipRepository));
        _eventPublisher = eventPublisher ?? throw new ArgumentNullException(nameof(eventPublisher));
    }

    public async Task<DeviceResponse> HandleAsync(
        CurrentUser user,
        string deviceId,
        UpdateDeviceStateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(deviceId))
            throw new ArgumentException("DeviceId is required.", nameof(deviceId));

        AuthorizationGuard.RequireRole(user, "SmartNest.Owner", "SmartNest.Technician");

        var device = await _repository.GetAsync(deviceId, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Device '{deviceId}' was not found.");

        var ownerId = await _homeOwnershipRepository.GetOwnerIdAsync(device.HomeId, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Home '{device.HomeId}' was not found.");
        AuthorizationGuard.RequireOwnership(user, ownerId);

        device.UpdateState(request.Property, request.Value.ToDomain());

        await _repository.UpdateAsync(device, cancellationToken).ConfigureAwait(false);
        await _eventPublisher.PublishAllAsync(device.DomainEvents, user.UserId, cancellationToken).ConfigureAwait(false);
        device.ClearDomainEvents();

        DeviceMetrics.StateChanges.Add(1);

        return DeviceResponse.FromDomain(device);
    }
}
