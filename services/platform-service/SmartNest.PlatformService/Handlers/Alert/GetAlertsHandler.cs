using SmartNest.PlatformService.Dtos.Alert;
using SmartNest.PlatformService.Repositories.Alert;
using SmartNest.PlatformService.Repositories.Shared;
using SmartNest.Shared.Security;

namespace SmartNest.PlatformService.Handlers.Alert;

/// <summary>Handles <c>GET /alerts?homeId={homeId}</c>. Any authenticated caller who owns the home may list its alerts.</summary>
public sealed class GetAlertsHandler
{
    private readonly IAlertRepository _repository;
    private readonly IHomeOwnershipRepository _homeOwnershipRepository;

    public GetAlertsHandler(IAlertRepository repository, IHomeOwnershipRepository homeOwnershipRepository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _homeOwnershipRepository = homeOwnershipRepository ?? throw new ArgumentNullException(nameof(homeOwnershipRepository));
    }

    public async Task<IReadOnlyList<AlertResponse>> HandleAsync(CurrentUser user, string homeId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (string.IsNullOrWhiteSpace(homeId))
            throw new ArgumentException("HomeId is required.", nameof(homeId));

        var ownerId = await _homeOwnershipRepository.GetOwnerIdAsync(homeId, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Home '{homeId}' was not found.");
        AuthorizationGuard.RequireOwnership(user, ownerId);

        var alerts = await _repository.GetByHomeIdAsync(homeId, cancellationToken).ConfigureAwait(false);
        return alerts.Select(AlertResponse.FromDomain).ToList();
    }
}
