using SmartNest.PlatformService.Dtos.Alert;
using SmartNest.PlatformService.Repositories.Alert;
using SmartNest.PlatformService.Repositories.Shared;
using SmartNest.Shared.Security;

namespace SmartNest.PlatformService.Handlers.Alert;

/// <summary>Handles <c>POST /alerts/{id}/acknowledge</c>. Owner/Technician may acknowledge, and must own the home.</summary>
public sealed class AcknowledgeAlertHandler
{
    private readonly IAlertRepository _repository;
    private readonly IHomeOwnershipRepository _homeOwnershipRepository;

    public AcknowledgeAlertHandler(IAlertRepository repository, IHomeOwnershipRepository homeOwnershipRepository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _homeOwnershipRepository = homeOwnershipRepository ?? throw new ArgumentNullException(nameof(homeOwnershipRepository));
    }

    public async Task<AlertResponse> HandleAsync(CurrentUser user, string homeId, string alertId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (string.IsNullOrWhiteSpace(homeId))
            throw new ArgumentException("HomeId is required.", nameof(homeId));
        if (string.IsNullOrWhiteSpace(alertId))
            throw new ArgumentException("AlertId is required.", nameof(alertId));

        AuthorizationGuard.RequireRole(user, "SmartNest.Owner", "SmartNest.Technician");

        var ownerId = await _homeOwnershipRepository.GetOwnerIdAsync(homeId, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Home '{homeId}' was not found.");
        AuthorizationGuard.RequireOwnership(user, ownerId);

        var alert = await _repository.GetAsync(homeId, alertId, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Alert '{alertId}' was not found.");

        alert.Acknowledge();
        await _repository.UpdateAsync(alert, cancellationToken).ConfigureAwait(false);

        return AlertResponse.FromDomain(alert);
    }
}
