using SmartNest.HomeService.Events;
using SmartNest.HomeService.Repositories;
using SmartNest.Shared.Security;

namespace SmartNest.HomeService.Handlers;

/// <summary>Handles <c>DELETE /homes/{id}</c>. Requires Owner role + caller must own the home.</summary>
public sealed class DeleteHomeHandler
{
    private readonly IHomeRepository _repository;
    private readonly HomeEventPublisher _eventPublisher;

    public DeleteHomeHandler(IHomeRepository repository, HomeEventPublisher eventPublisher)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _eventPublisher = eventPublisher ?? throw new ArgumentNullException(nameof(eventPublisher));
    }

    public async Task HandleAsync(CurrentUser user, string homeId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (string.IsNullOrWhiteSpace(homeId))
            throw new ArgumentException("HomeId is required.", nameof(homeId));

        AuthorizationGuard.RequireRole(user, "SmartNest.Owner");

        var home = await _repository.GetAsync(homeId, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Home '{homeId}' was not found.");

        AuthorizationGuard.RequireOwnership(user, home.OwnerId);

        home.MarkDeleted();

        var deleted = await _repository.DeleteAsync(homeId, cancellationToken).ConfigureAwait(false);
        if (!deleted)
            throw new KeyNotFoundException($"Home '{homeId}' was not found.");

        await _eventPublisher.PublishAllAsync(home.DomainEvents, user.UserId, cancellationToken).ConfigureAwait(false);
        home.ClearDomainEvents();
    }
}
