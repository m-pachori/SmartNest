using SmartNest.IdentityService.Domain;
using SmartNest.IdentityService.Dtos;
using SmartNest.IdentityService.Events;
using SmartNest.IdentityService.Repositories;
using SmartNest.Shared.Security;

namespace SmartNest.IdentityService.Handlers;

/// <summary>
/// Handles <c>POST /homes/{homeId}/users/invite</c>. Only the home's Owner may invite
/// members (verified against the Cosmos-level <c>homes</c> document's OwnerId).
/// </summary>
public sealed class InviteUserHandler
{
    private readonly IIdentityRepository _repository;
    private readonly IHomeOwnershipRepository _homeOwnershipRepository;
    private readonly IdentityEventPublisher _eventPublisher;

    public InviteUserHandler(
        IIdentityRepository repository,
        IHomeOwnershipRepository homeOwnershipRepository,
        IdentityEventPublisher eventPublisher)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _homeOwnershipRepository = homeOwnershipRepository ?? throw new ArgumentNullException(nameof(homeOwnershipRepository));
        _eventPublisher = eventPublisher ?? throw new ArgumentNullException(nameof(eventPublisher));
    }

    public async Task<MembershipResponse> HandleAsync(
        CurrentUser user,
        string homeId,
        InviteUserRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(homeId))
            throw new ArgumentException("HomeId is required.", nameof(homeId));

        AuthorizationGuard.RequireRole(user, "SmartNest.Owner");

        var ownerId = await _homeOwnershipRepository.GetOwnerIdAsync(homeId, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Home '{homeId}' was not found.");
        AuthorizationGuard.RequireOwnership(user, ownerId);

        var existing = await _repository.GetByHomeAndUserAsync(homeId, request.TargetUserId, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
            throw new InvalidOperationException($"User '{request.TargetUserId}' already has an active membership for home '{homeId}'.");

        var membership = HomeMembership.Invite(homeId, request.TargetUserId, request.Role, user.UserId);

        await _repository.CreateAsync(membership, cancellationToken).ConfigureAwait(false);
        await _eventPublisher.PublishAllAsync(membership.DomainEvents, user.UserId, cancellationToken).ConfigureAwait(false);
        membership.ClearDomainEvents();

        return MembershipResponse.FromDomain(membership);
    }
}
