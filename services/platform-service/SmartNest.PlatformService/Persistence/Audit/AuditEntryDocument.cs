using Newtonsoft.Json;

namespace SmartNest.PlatformService.Persistence.Audit;

/// <summary>
/// Cosmos DB persistence model for the <c>audit-log</c> container (partition key
/// <c>/aggregateId</c>, already provisioned in Task 1). Append-only - no updates or
/// deletes. Fields per smartnest-plan.md Task 7.
/// </summary>
public sealed class AuditEntryDocument
{
    /// <summary>Document id == <see cref="EventId"/>.</summary>
    [JsonProperty("id")]
    public string Id { get; set; } = default!;

    [JsonProperty("aggregateId")]
    public string AggregateId { get; set; } = default!;

    [JsonProperty("eventId")]
    public string EventId { get; set; } = default!;

    [JsonProperty("eventType")]
    public string EventType { get; set; } = default!;

    [JsonProperty("aggregateType")]
    public string AggregateType { get; set; } = default!;

    [JsonProperty("occurredAt")]
    public DateTimeOffset OccurredAt { get; set; }

    [JsonProperty("actorId")]
    public string ActorId { get; set; } = default!;

    [JsonProperty("homeId")]
    public string HomeId { get; set; } = default!;

    [JsonProperty("correlationId")]
    public string CorrelationId { get; set; } = default!;

    /// <summary>Raw JSON of the event-specific payload (kept opaque - Audit doesn't need to know every event's shape).</summary>
    [JsonProperty("payload")]
    public string Payload { get; set; } = default!;

    /// <summary>
    /// Assigned atomically by <see cref="Repositories.Audit.CosmosAuditRepository.AppendAsync"/>
    /// via a Cosmos DB transactional batch with the paired <see cref="SequenceCounterDocument"/>.
    /// </summary>
    [JsonProperty("sequenceNumber")]
    public int SequenceNumber { get; set; }
}
