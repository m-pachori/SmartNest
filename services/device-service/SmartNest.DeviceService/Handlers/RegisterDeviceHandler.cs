using SmartNest.DeviceService.Domain;
using SmartNest.DeviceService.Domain.ValueObjects;
using SmartNest.DeviceService.Dtos;
using SmartNest.DeviceService.Events;
using SmartNest.DeviceService.Repositories;
using SmartNest.Shared.Security;

namespace SmartNest.DeviceService.Handlers;

/// <summary>
/// Handles <c>POST /homes/{homeId}/devices</c>. Owner/Technician may register a device
/// (treated as "mutate" per smartnest-plan.md Task 3); caller must own the target home
/// (verified against the Cosmos-level <c>homes</c> document's OwnerId, not the JWT's
/// self-asserted <c>homeId</c> claim - mirrors Home Service's ownership pattern).
/// </summary>
public sealed class RegisterDeviceHandler
{
    private readonly IDeviceRepository _repository;
    private readonly IHomeOwnershipRepository _homeOwnershipRepository;
    private readonly DeviceEventPublisher _eventPublisher;

    public RegisterDeviceHandler(
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
        string homeId,
        RegisterDeviceRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(homeId))
            throw new ArgumentException("HomeId is required.", nameof(homeId));

        AuthorizationGuard.RequireRole(user, "SmartNest.Owner", "SmartNest.Technician");

        var ownerId = await _homeOwnershipRepository.GetOwnerIdAsync(homeId, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Home '{homeId}' was not found.");
        AuthorizationGuard.RequireOwnership(user, ownerId);

        var metadata = new DeviceMetadata(request.Name, request.DeviceType, request.Manufacturer, request.Model);
        var device = Device.Register(homeId, metadata);

        await _repository.CreateAsync(device, cancellationToken).ConfigureAwait(false);
        await _eventPublisher.PublishAllAsync(device.DomainEvents, user.UserId, cancellationToken).ConfigureAwait(false);
        device.ClearDomainEvents();

        return DeviceResponse.FromDomain(device);
    }
}
