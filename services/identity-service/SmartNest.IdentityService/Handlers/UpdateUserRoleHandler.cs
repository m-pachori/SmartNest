using SmartNest.IdentityService.Dtos;
using SmartNest.IdentityService.Events;
using SmartNest.IdentityService.Repositories;
using SmartNest.Shared.Security;

namespace SmartNest.IdentityService.Handlers;

/// <summary>
/// Handles <c>PUT /users/{id}/role</c>. Only the home's Owner may change a member's role
/// (verified against the Cosmos-level <c>homes</c> document's OwnerId).
/// </summary>
public sealed class UpdateUserRoleHandler
{
    private readonly IIdentityRepository _repository;
    private readonly IHomeOwnershipRepository _homeOwnershipRepository;
    private readonly IdentityEventPublisher _eventPublisher;

    public UpdateUserRoleHandler(
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
        string membershipId,
        UpdateUserRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(membershipId))
            throw new ArgumentException("MembershipId is required.", nameof(membershipId));

        AuthorizationGuard.RequireRole(user, "SmartNest.Owner");

        var membership = await _repository.GetAsync(membershipId, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Membership '{membershipId}' was not found.");

        var ownerId = await _homeOwnershipRepository.GetOwnerIdAsync(membership.HomeId, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Home '{membership.HomeId}' was not found.");
        AuthorizationGuard.RequireOwnership(user, ownerId);

        membership.AssignRole(request.Role, user.UserId);

        await _repository.UpdateAsync(membership, cancellationToken).ConfigureAwait(false);
        await _eventPublisher.PublishAllAsync(membership.DomainEvents, user.UserId, cancellationToken).ConfigureAwait(false);
        membership.ClearDomainEvents();

        return MembershipResponse.FromDomain(membership);
    }
}
