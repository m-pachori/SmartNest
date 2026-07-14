namespace SmartNest.Shared.Events;

/// <summary>
/// Standard domain event envelope used by every SmartNest microservice when publishing
/// to Service Bus topics. Matches the schema defined in smartnest-plan.md's
/// "Event Sourcing Strategy" section so the Audit Service can consume any event type
/// without breaking changes.
/// </summary>
/// <typeparam name="TPayload">The event-type-specific payload shape.</typeparam>
public sealed record EventEnvelope<TPayload>(
    string EventId,
    string EventType,
    string AggregateId,
    string AggregateType,
    DateTimeOffset OccurredAt,
    string ActorId,
    string HomeId,
    string CorrelationId,
    TPayload Payload)
{
    /// <summary>
    /// Creates a new envelope with a generated <see cref="EventId"/>, the current UTC
    /// timestamp, and a generated <see cref="CorrelationId"/> if one isn't supplied.
    /// </summary>
    public static EventEnvelope<TPayload> Create(
        string eventType,
        string aggregateId,
        string aggregateType,
        string actorId,
        string homeId,
        TPayload payload,
        string? correlationId = null)
    {
        if (string.IsNullOrWhiteSpace(eventType))
            throw new ArgumentException("Event type is required.", nameof(eventType));
        if (string.IsNullOrWhiteSpace(aggregateId))
            throw new ArgumentException("Aggregate id is required.", nameof(aggregateId));
        if (string.IsNullOrWhiteSpace(aggregateType))
            throw new ArgumentException("Aggregate type is required.", nameof(aggregateType));
        if (string.IsNullOrWhiteSpace(actorId))
            throw new ArgumentException("Actor id is required.", nameof(actorId));
        if (string.IsNullOrWhiteSpace(homeId))
            throw new ArgumentException("Home id is required.", nameof(homeId));
        ArgumentNullException.ThrowIfNull(payload);

        return new EventEnvelope<TPayload>(
            EventId: Guid.NewGuid().ToString(),
            EventType: eventType,
            AggregateId: aggregateId,
            AggregateType: aggregateType,
            OccurredAt: DateTimeOffset.UtcNow,
            ActorId: actorId,
            HomeId: homeId,
            CorrelationId: string.IsNullOrWhiteSpace(correlationId) ? Guid.NewGuid().ToString() : correlationId,
            Payload: payload);
    }
}
