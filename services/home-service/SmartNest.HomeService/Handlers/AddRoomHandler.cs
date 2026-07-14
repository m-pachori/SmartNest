using SmartNest.HomeService.Dtos;
using SmartNest.HomeService.Events;
using SmartNest.HomeService.Repositories;
using SmartNest.Shared.Security;

namespace SmartNest.HomeService.Handlers;

/// <summary>Handles <c>POST /homes/{id}/rooms</c>. Requires Owner role + matching homeId claim.</summary>
public sealed class AddRoomHandler
{
    private readonly IHomeRepository _repository;
    private readonly HomeEventPublisher _eventPublisher;

    public AddRoomHandler(IHomeRepository repository, HomeEventPublisher eventPublisher)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _eventPublisher = eventPublisher ?? throw new ArgumentNullException(nameof(eventPublisher));
    }

    public async Task<RoomResponse> HandleAsync(
        CurrentUser user,
        string homeId,
        AddRoomRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(homeId))
            throw new ArgumentException("HomeId is required.", nameof(homeId));

        AuthorizationGuard.RequireRole(user, "SmartNest.Owner");
        AuthorizationGuard.RequireHomeIdMatch(user, homeId);

        var home = await _repository.GetAsync(homeId, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Home '{homeId}' was not found.");

        var room = home.AddRoom(request.Name, request.RoomType);

        await _repository.UpdateAsync(home, cancellationToken).ConfigureAwait(false);
        await _eventPublisher.PublishAllAsync(home.DomainEvents, user.UserId, cancellationToken).ConfigureAwait(false);
        home.ClearDomainEvents();

        return new RoomResponse(room.RoomId, room.Name, room.RoomType);
    }
}
