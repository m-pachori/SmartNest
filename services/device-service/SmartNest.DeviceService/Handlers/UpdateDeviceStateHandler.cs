using SmartNest.DeviceService.Dtos;
using SmartNest.DeviceService.Events;
using SmartNest.DeviceService.Repositories;
using SmartNest.DeviceService.Telemetry;
using SmartNest.Shared.Security;

namespace SmartNest.DeviceService.Handlers;

/// <summary>
/// Handles <c>PATCH /devices/{id}/state</c>. Owner/Technician may mutate; caller's
/// <c>homeId</c> claim must match the device's home. Increments the
/// <c>device.state.changes</c> custom metric on every successful update.
/// </summary>
public sealed class UpdateDeviceStateHandler
{
    private readonly IDeviceRepository _repository;
    private readonly DeviceEventPublisher _eventPublisher;

    public UpdateDeviceStateHandler(IDeviceRepository repository, DeviceEventPublisher eventPublisher)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
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

        AuthorizationGuard.RequireHomeIdMatch(user, device.HomeId);

        device.UpdateState(request.Property, request.Value.ToDomain());

        await _repository.UpdateAsync(device, cancellationToken).ConfigureAwait(false);
        await _eventPublisher.PublishAllAsync(device.DomainEvents, user.UserId, cancellationToken).ConfigureAwait(false);
        device.ClearDomainEvents();

        DeviceMetrics.StateChanges.Add(1);

        return DeviceResponse.FromDomain(device);
    }
}
