using SmartNest.DeviceService.Events;
using SmartNest.DeviceService.Repositories;
using SmartNest.Shared.Security;

namespace SmartNest.DeviceService.Handlers;

/// <summary>
/// Handles <c>DELETE /devices/{id}</c>. Owner/Technician may remove; caller's
/// <c>homeId</c> claim must match the device's home.
/// </summary>
public sealed class RemoveDeviceHandler
{
    private readonly IDeviceRepository _repository;
    private readonly DeviceEventPublisher _eventPublisher;

    public RemoveDeviceHandler(IDeviceRepository repository, DeviceEventPublisher eventPublisher)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _eventPublisher = eventPublisher ?? throw new ArgumentNullException(nameof(eventPublisher));
    }

    public async Task HandleAsync(CurrentUser user, string deviceId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (string.IsNullOrWhiteSpace(deviceId))
            throw new ArgumentException("DeviceId is required.", nameof(deviceId));

        AuthorizationGuard.RequireRole(user, "SmartNest.Owner", "SmartNest.Technician");

        var device = await _repository.GetAsync(deviceId, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Device '{deviceId}' was not found.");

        AuthorizationGuard.RequireHomeIdMatch(user, device.HomeId);

        device.MarkRemoved();

        var deleted = await _repository.DeleteAsync(deviceId, device.HomeId, cancellationToken).ConfigureAwait(false);
        if (!deleted)
            throw new KeyNotFoundException($"Device '{deviceId}' was not found.");

        await _eventPublisher.PublishAllAsync(device.DomainEvents, user.UserId, cancellationToken).ConfigureAwait(false);
        device.ClearDomainEvents();
    }
}
