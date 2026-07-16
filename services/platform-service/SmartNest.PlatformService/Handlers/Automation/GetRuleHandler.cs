using SmartNest.PlatformService.Dtos.Automation;
using SmartNest.PlatformService.Repositories.Automation;
using SmartNest.PlatformService.Repositories.Shared;
using SmartNest.Shared.Security;

namespace SmartNest.PlatformService.Handlers.Automation;

/// <summary>Handles <c>GET /homes/{homeId}/rules/{id}</c>. Any authenticated caller who owns the home may read.</summary>
public sealed class GetRuleHandler
{
    private readonly IRuleRepository _repository;
    private readonly IHomeOwnershipRepository _homeOwnershipRepository;

    public GetRuleHandler(IRuleRepository repository, IHomeOwnershipRepository homeOwnershipRepository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _homeOwnershipRepository = homeOwnershipRepository ?? throw new ArgumentNullException(nameof(homeOwnershipRepository));
    }

    public async Task<RuleResponse> HandleAsync(CurrentUser user, string homeId, string ruleId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (string.IsNullOrWhiteSpace(homeId))
            throw new ArgumentException("HomeId is required.", nameof(homeId));
        if (string.IsNullOrWhiteSpace(ruleId))
            throw new ArgumentException("RuleId is required.", nameof(ruleId));

        var ownerId = await _homeOwnershipRepository.GetOwnerIdAsync(homeId, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Home '{homeId}' was not found.");
        AuthorizationGuard.RequireOwnership(user, ownerId);

        var rule = await _repository.GetAsync(homeId, ruleId, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Rule '{ruleId}' was not found.");

        return RuleResponse.FromDomain(rule);
    }
}
