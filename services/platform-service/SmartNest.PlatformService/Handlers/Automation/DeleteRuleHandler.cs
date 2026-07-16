using SmartNest.PlatformService.Repositories.Automation;
using SmartNest.PlatformService.Repositories.Shared;
using SmartNest.Shared.Security;

namespace SmartNest.PlatformService.Handlers.Automation;

/// <summary>Handles <c>DELETE /homes/{homeId}/rules/{id}</c>. Requires the Owner role + home ownership.</summary>
public sealed class DeleteRuleHandler
{
    private readonly IRuleRepository _repository;
    private readonly IHomeOwnershipRepository _homeOwnershipRepository;

    public DeleteRuleHandler(IRuleRepository repository, IHomeOwnershipRepository homeOwnershipRepository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _homeOwnershipRepository = homeOwnershipRepository ?? throw new ArgumentNullException(nameof(homeOwnershipRepository));
    }

    public async Task HandleAsync(CurrentUser user, string homeId, string ruleId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (string.IsNullOrWhiteSpace(homeId))
            throw new ArgumentException("HomeId is required.", nameof(homeId));
        if (string.IsNullOrWhiteSpace(ruleId))
            throw new ArgumentException("RuleId is required.", nameof(ruleId));

        AuthorizationGuard.RequireRole(user, "SmartNest.Owner");

        var ownerId = await _homeOwnershipRepository.GetOwnerIdAsync(homeId, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Home '{homeId}' was not found.");
        AuthorizationGuard.RequireOwnership(user, ownerId);

        var deleted = await _repository.DeleteAsync(homeId, ruleId, cancellationToken).ConfigureAwait(false);
        if (!deleted)
            throw new KeyNotFoundException($"Rule '{ruleId}' was not found.");
    }
}
