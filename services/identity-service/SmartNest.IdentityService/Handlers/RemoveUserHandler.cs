using SmartNest.IdentityService.Events;
using SmartNest.IdentityService.Repositories;
using SmartNest.Shared.Security;

namespace SmartNest.IdentityService.Handlers;

/// <summary>
/// Handles <c>DELETE /homes/{homeId}/users/{userId}</c>. Only the home's Owner may
/// deactivate a member. Soft-delete only (<c>Deactivate()</c>) - the Cosmos document is
/// retained for the Audit Service, matching the <c>UserDeactivated</c> event name.
/// </summary>
public sealed class RemoveUserHandler
{
    private readonly IIdentityRepository _repository;
    private readonly IHomeOwnershipRepository _homeOwnershipRepository;
    private readonly IdentityEventPublisher _eventPublisher;

    public RemoveUserHandler(
        IIdentityRepository repository,
        IHomeOwnershipRepository homeOwnershipRepository,
        IdentityEventPublisher eventPublisher)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _homeOwnershipRepository = homeOwnershipRepository ?? throw new ArgumentNullException(nameof(homeOwnershipRepository));
        _eventPublisher = eventPublisher ?? throw new ArgumentNullException(nameof(eventPublisher));
    }

    public async Task HandleAsync(
        CurrentUser user,
        string homeId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (string.IsNullOrWhiteSpace(homeId))
            throw new ArgumentException("HomeId is required.", nameof(homeId));
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("UserId is required.", nameof(userId));

        AuthorizationGuard.RequireRole(user, "SmartNest.Owner");

        var ownerId = await _homeOwnershipRepository.GetOwnerIdAsync(homeId, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Home '{homeId}' was not found.");
        AuthorizationGuard.RequireOwnership(user, ownerId);

        var membership = await _repository.GetByHomeAndUserAsync(homeId, userId, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"No active membership found for user '{userId}' in home '{homeId}'.");

        membership.Deactivate();

        await _repository.UpdateAsync(membership, cancellationToken).ConfigureAwait(false);
        await _eventPublisher.PublishAllAsync(membership.DomainEvents, user.UserId, cancellationToken).ConfigureAwait(false);
        membership.ClearDomainEvents();
    }
}
