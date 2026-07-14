namespace SmartNest.Shared.Events;

/// <summary>
/// Publishes domain event envelopes to a messaging backend (Azure Service Bus topic).
/// </summary>
public interface IEventPublisher
{
    /// <summary>
    /// Publishes <paramref name="envelope"/> to the given topic.
    /// </summary>
    Task PublishAsync<TPayload>(
        string topicName,
        EventEnvelope<TPayload> envelope,
        CancellationToken cancellationToken = default);
}
