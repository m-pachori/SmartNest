using Microsoft.Azure.Cosmos;
using SmartNest.PlatformService.Persistence.Summary;
using SmartNest.Shared.Persistence;

namespace SmartNest.PlatformService.Repositories.Summary;

/// <summary>
/// Cosmos DB-backed <see cref="ISummaryRepository"/>. Container: <c>summaries</c>,
/// partition key: <c>/homeId</c> (already provisioned in Task 1).
/// </summary>
internal sealed class CosmosSummaryRepository : CosmosRepositoryBase<DailySummaryDocument>, ISummaryRepository
{
    public CosmosSummaryRepository(Container container) : base(container)
    {
    }

    public Task UpsertAsync(DailySummaryDocument summary, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(summary);

        return UpsertAsync(summary, partitionKeyValue: summary.HomeId, cancellationToken);
    }

    public Task<DailySummaryDocument?> GetAsync(string homeId, string date, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(homeId))
            throw new ArgumentException("HomeId is required.", nameof(homeId));
        if (string.IsNullOrWhiteSpace(date))
            throw new ArgumentException("Date is required.", nameof(date));

        var id = $"{homeId}_{date}";
        return GetAsync(id, partitionKeyValue: homeId, cancellationToken);
    }
}
