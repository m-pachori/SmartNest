using SmartNest.PlatformService.Dtos.Audit;
using SmartNest.PlatformService.Repositories.Audit;
using SmartNest.Shared.Security;

namespace SmartNest.PlatformService.Handlers.Audit;

/// <summary>Handles <c>GET /audit/{aggregateId}?from={sequence}</c>. Owner-only (event-sourcing internals).</summary>
public sealed class GetAuditLogHandler
{
    private readonly IAuditRepository _repository;

    public GetAuditLogHandler(IAuditRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<IReadOnlyList<AuditEntryResponse>> HandleAsync(
        CurrentUser user,
        string aggregateId,
        int fromSequence,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (string.IsNullOrWhiteSpace(aggregateId))
            throw new ArgumentException("AggregateId is required.", nameof(aggregateId));

        AuthorizationGuard.RequireRole(user, "SmartNest.Owner");

        var entries = await _repository.GetByAggregateAsync(aggregateId, fromSequence, cancellationToken).ConfigureAwait(false);
        return entries.Select(AuditEntryResponse.FromDocument).ToList();
    }
}
