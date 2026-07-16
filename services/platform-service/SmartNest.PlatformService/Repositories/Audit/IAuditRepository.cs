using SmartNest.PlatformService.Persistence.Audit;

namespace SmartNest.PlatformService.Repositories.Audit;

public interface IAuditRepository
{
    /// <summary>
    /// Atomically assigns the next <c>sequenceNumber</c> for <paramref name="entry"/>'s
    /// <c>AggregateId</c> and appends it, returning the assigned sequence number.
    /// </summary>
    Task<int> AppendAsync(AuditEntryDocument entry, CancellationToken cancellationToken = default);

    /// <summary>Returns audit entries for an aggregate, ordered by sequence number, optionally starting from <paramref name="fromSequence"/>.</summary>
    Task<IReadOnlyList<AuditEntryDocument>> GetByAggregateAsync(string aggregateId, int fromSequence = 0, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cross-partition query returning the distinct <c>homeId</c>s with audit activity in
    /// <c>[from, to)</c>. Used by the Summary bounded context's nightly job (Task 8) to
    /// discover which homes need a <c>DailySummary</c>.
    /// </summary>
    Task<IReadOnlyList<string>> GetDistinctHomeIdsAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cross-partition query returning every audit entry for a home in <c>[from, to)</c> -
    /// used by the Summary bounded context to aggregate event counts (Task 8).
    /// </summary>
    Task<IReadOnlyList<AuditEntryDocument>> GetByHomeAndDateRangeAsync(string homeId, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default);
}
