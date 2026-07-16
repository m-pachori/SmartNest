using SmartNest.PlatformService.Dtos.Audit;
using SmartNest.PlatformService.Repositories.Audit;
using SmartNest.Shared.Security;

namespace SmartNest.PlatformService.Handlers.Audit;

/// <summary>
/// Handles <c>POST /audit/replay/{aggregateId}</c>. Returns the full ordered event stream
/// for state reconstruction by the caller (per smartnest-plan.md Task 7's Event Sourcing
/// Strategy - the Audit Service is the source of truth for the ordered stream; the caller
/// reconstructs aggregate state from it). Enforces Owner role inside the handler itself,
/// not just via APIM policy, per Task 7's explicit requirement.
/// </summary>
public sealed class ReplayEventsHandler
{
    private readonly IAuditRepository _repository;

    public ReplayEventsHandler(IAuditRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<IReadOnlyList<AuditEntryResponse>> HandleAsync(CurrentUser user, string aggregateId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (string.IsNullOrWhiteSpace(aggregateId))
            throw new ArgumentException("AggregateId is required.", nameof(aggregateId));

        AuthorizationGuard.RequireRole(user, "SmartNest.Owner");

        var entries = await _repository.GetByAggregateAsync(aggregateId, fromSequence: 0, cancellationToken).ConfigureAwait(false);
        return entries.Select(AuditEntryResponse.FromDocument).ToList();
    }
}
