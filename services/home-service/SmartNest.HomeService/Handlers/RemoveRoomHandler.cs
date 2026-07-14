using SmartNest.HomeService.Repositories;
using SmartNest.Shared.Security;

namespace SmartNest.HomeService.Handlers;

/// <summary>
/// Handles <c>DELETE /homes/{id}/rooms/{roomId}</c>. Requires Owner role + matching homeId claim.
/// No domain event is published — only HomeCreated, RoomAdded, and HomeDeleted are defined
/// in the platform's event catalogue for the Home bounded context (see smartnest-plan.md).
/// </summary>
public sealed class RemoveRoomHandler
{
    private readonly IHomeRepository _repository;

    public RemoveRoomHandler(IHomeRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task HandleAsync(
        CurrentUser user,
        string homeId,
        string roomId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (string.IsNullOrWhiteSpace(homeId))
            throw new ArgumentException("HomeId is required.", nameof(homeId));
        if (string.IsNullOrWhiteSpace(roomId))
            throw new ArgumentException("RoomId is required.", nameof(roomId));

        AuthorizationGuard.RequireRole(user, "SmartNest.Owner");
        AuthorizationGuard.RequireHomeIdMatch(user, homeId);

        var home = await _repository.GetAsync(homeId, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Home '{homeId}' was not found.");

        home.RemoveRoom(roomId);

        await _repository.UpdateAsync(home, cancellationToken).ConfigureAwait(false);
    }
}
