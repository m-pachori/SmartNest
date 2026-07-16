using System.Net;
using Microsoft.Azure.Cosmos;
using SmartNest.PlatformService.Persistence.Audit;

namespace SmartNest.PlatformService.Repositories.Audit;

/// <summary>
/// Cosmos DB-backed <see cref="IAuditRepository"/>. Container: <c>audit-log</c>, partition
/// key: <c>/aggregateId</c> (already provisioned in Task 1). Doesn't extend
/// <see cref="Shared.Persistence.CosmosRepositoryBase{T}"/> - Audit's access pattern
/// (atomic append via transactional batch + range query) doesn't fit the generic
/// get/create/upsert/delete shape the other services' repositories use.
/// </summary>
internal sealed class CosmosAuditRepository : IAuditRepository
{
    /// <summary>Fixed id for the per-partition <see cref="SequenceCounterDocument"/>.</summary>
    public const string CounterDocumentId = "counter";

    private const int MaxAttempts = 5;

    private readonly Container _container;

    public CosmosAuditRepository(Container container)
    {
        _container = container ?? throw new ArgumentNullException(nameof(container));
    }

    public async Task<int> AppendAsync(AuditEntryDocument entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (string.IsNullOrWhiteSpace(entry.AggregateId))
            throw new ArgumentException("AggregateId is required.", nameof(entry));

        var partitionKey = new PartitionKey(entry.AggregateId);

        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var (lastSequence, etag) = await ReadCounterAsync(entry.AggregateId, partitionKey, cancellationToken).ConfigureAwait(false);
            var nextSequence = lastSequence + 1;
            entry.SequenceNumber = nextSequence;

            var counterDoc = new SequenceCounterDocument
            {
                Id = CounterDocumentId,
                AggregateId = entry.AggregateId,
                LastSequence = nextSequence,
            };

            var batch = _container.CreateTransactionalBatch(partitionKey);
            if (etag is null)
            {
                batch.CreateItem(counterDoc);
            }
            else
            {
                batch.ReplaceItem(CounterDocumentId, counterDoc, new TransactionalBatchItemRequestOptions { IfMatchEtag = etag });
            }
            batch.CreateItem(entry);

            using var response = await batch.ExecuteAsync(cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
                return nextSequence;

            // Counter created/updated concurrently by another append - retry with a fresh read.
            if (response.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.PreconditionFailed)
                continue;

            throw new InvalidOperationException($"Failed to append audit entry for aggregate '{entry.AggregateId}': {response.StatusCode}.");
        }

        throw new InvalidOperationException(
            $"Failed to append audit entry for aggregate '{entry.AggregateId}' after {MaxAttempts} attempts due to concurrent writes.");
    }

    public async Task<IReadOnlyList<AuditEntryDocument>> GetByAggregateAsync(string aggregateId, int fromSequence = 0, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(aggregateId))
            throw new ArgumentException("AggregateId is required.", nameof(aggregateId));

        var query = new QueryDefinition(
                "SELECT * FROM c WHERE c.aggregateId = @aggregateId AND IS_DEFINED(c.sequenceNumber) AND c.sequenceNumber >= @fromSequence ORDER BY c.sequenceNumber")
            .WithParameter("@aggregateId", aggregateId)
            .WithParameter("@fromSequence", fromSequence);

        var results = new List<AuditEntryDocument>();
        using var iterator = _container.GetItemQueryIterator<AuditEntryDocument>(query, requestOptions: new QueryRequestOptions
        {
            PartitionKey = new PartitionKey(aggregateId),
        });

        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
            results.AddRange(page);
        }

        return results;
    }

    public async Task<IReadOnlyList<string>> GetDistinctHomeIdsAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition(
                "SELECT DISTINCT VALUE c.homeId FROM c WHERE IS_DEFINED(c.sequenceNumber) AND c.occurredAt >= @from AND c.occurredAt < @to")
            .WithParameter("@from", from)
            .WithParameter("@to", to);

        var results = new List<string>();
        using var iterator = _container.GetItemQueryIterator<string>(query);
        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
            results.AddRange(page);
        }

        return results;
    }

    public async Task<IReadOnlyList<AuditEntryDocument>> GetByHomeAndDateRangeAsync(string homeId, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(homeId))
            throw new ArgumentException("HomeId is required.", nameof(homeId));

        var query = new QueryDefinition(
                "SELECT * FROM c WHERE c.homeId = @homeId AND IS_DEFINED(c.sequenceNumber) AND c.occurredAt >= @from AND c.occurredAt < @to")
            .WithParameter("@homeId", homeId)
            .WithParameter("@from", from)
            .WithParameter("@to", to);

        var results = new List<AuditEntryDocument>();
        using var iterator = _container.GetItemQueryIterator<AuditEntryDocument>(query);
        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
            results.AddRange(page);
        }

        return results;
    }

    private async Task<(int lastSequence, string? etag)> ReadCounterAsync(string aggregateId, PartitionKey partitionKey, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _container
                .ReadItemAsync<SequenceCounterDocument>(CounterDocumentId, partitionKey, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            return (response.Resource.LastSequence, response.ETag);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return (0, null);
        }
    }
}
