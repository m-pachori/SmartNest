using Newtonsoft.Json;

namespace SmartNest.PlatformService.Persistence.Audit;

/// <summary>
/// One per <c>aggregateId</c>, sharing the same partition key as its
/// <see cref="AuditEntryDocument"/> entries (required for the Cosmos DB transactional
/// batch in <see cref="Repositories.Audit.CosmosAuditRepository.AppendAsync"/> to work -
/// Cosmos transactions are partition-scoped). Fixed document id
/// (<see cref="Repositories.Audit.CosmosAuditRepository.CounterDocumentId"/>) so it can be
/// point-read/replaced without a query.
/// </summary>
internal sealed class SequenceCounterDocument
{
    [JsonProperty("id")]
    public string Id { get; set; } = default!;

    [JsonProperty("aggregateId")]
    public string AggregateId { get; set; } = default!;

    public int LastSequence { get; set; }
}
