using SmartNest.PlatformService.Dtos.Automation;
using SmartNest.PlatformService.Repositories.Automation;
using SmartNest.PlatformService.Repositories.Shared;
using SmartNest.Shared.Security;

namespace SmartNest.PlatformService.Handlers.Automation;

/// <summary>Handles <c>PUT /homes/{homeId}/rules/{id}</c>. Requires the Owner role + home ownership.</summary>
public sealed class UpdateRuleHandler
{
    private readonly IRuleRepository _repository;
    private readonly IHomeOwnershipRepository _homeOwnershipRepository;

    public UpdateRuleHandler(IRuleRepository repository, IHomeOwnershipRepository homeOwnershipRepository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _homeOwnershipRepository = homeOwnershipRepository ?? throw new ArgumentNullException(nameof(homeOwnershipRepository));
    }

    public async Task<RuleResponse> HandleAsync(CurrentUser user, string homeId, string ruleId, UpdateRuleRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(homeId))
            throw new ArgumentException("HomeId is required.", nameof(homeId));
        if (string.IsNullOrWhiteSpace(ruleId))
            throw new ArgumentException("RuleId is required.", nameof(ruleId));

        AuthorizationGuard.RequireRole(user, "SmartNest.Owner");

        var ownerId = await _homeOwnershipRepository.GetOwnerIdAsync(homeId, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Home '{homeId}' was not found.");
        AuthorizationGuard.RequireOwnership(user, ownerId);

        var rule = await _repository.GetAsync(homeId, ruleId, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Rule '{ruleId}' was not found.");

        rule.UpdateDetails(request.Name, request.Condition.ToDomain(), request.Action.ToDomain(), request.Enabled);
        await _repository.UpdateAsync(rule, cancellationToken).ConfigureAwait(false);

        return RuleResponse.FromDomain(rule);
    }
}
