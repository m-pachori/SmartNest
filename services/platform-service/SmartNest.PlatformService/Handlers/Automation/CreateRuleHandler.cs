using SmartNest.PlatformService.Domain.Automation;
using SmartNest.PlatformService.Dtos.Automation;
using SmartNest.PlatformService.Repositories.Automation;
using SmartNest.PlatformService.Repositories.Shared;
using SmartNest.Shared.Security;

namespace SmartNest.PlatformService.Handlers.Automation;

/// <summary>Handles <c>POST /rules</c>. Requires the Owner role + home ownership.</summary>
public sealed class CreateRuleHandler
{
    private readonly IRuleRepository _repository;
    private readonly IHomeOwnershipRepository _homeOwnershipRepository;

    public CreateRuleHandler(IRuleRepository repository, IHomeOwnershipRepository homeOwnershipRepository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _homeOwnershipRepository = homeOwnershipRepository ?? throw new ArgumentNullException(nameof(homeOwnershipRepository));
    }

    public async Task<RuleResponse> HandleAsync(CurrentUser user, string homeId, CreateRuleRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(homeId))
            throw new ArgumentException("HomeId is required.", nameof(homeId));

        AuthorizationGuard.RequireRole(user, "SmartNest.Owner");

        var ownerId = await _homeOwnershipRepository.GetOwnerIdAsync(homeId, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Home '{homeId}' was not found.");
        AuthorizationGuard.RequireOwnership(user, ownerId);

        var rule = Rule.Create(homeId, request.DeviceId, request.Name, request.Condition.ToDomain(), request.Action.ToDomain());
        await _repository.CreateAsync(rule, cancellationToken).ConfigureAwait(false);

        return RuleResponse.FromDomain(rule);
    }
}
