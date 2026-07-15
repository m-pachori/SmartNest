using SmartNest.DeviceService.Domain.Events;
using SmartNest.Shared.Events;

namespace SmartNest.DeviceService.Events;

/// <summary>
/// Translates Device Service domain events into the shared <see cref="EventEnvelope{TPayload}"/>
/// and publishes them to the <c>device-events</c> Service Bus topic, using the least-privilege
/// send-only connection string (see infra/main.bicep - DeviceServiceSend authorization rule).
/// </summary>
public sealed class DeviceEventPublisher
{
    public const string TopicName = "device-events";

    private readonly IEventPublisher _eventPublisher;

    public DeviceEventPublisher(IEventPublisher eventPublisher)
    {
        _eventPublisher = eventPublisher ?? throw new ArgumentNullException(nameof(eventPublisher));
    }

    /// <summary>
    /// Publishes every domain event on <paramref name="domainEvents"/> to <c>device-events</c>,
    /// attributing the events to <paramref name="actorId"/>.
    /// </summary>
    public async Task PublishAllAsync(
        IEnumerable<IDomainEvent> domainEvents,
        string actorId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvents);
        if (string.IsNullOrWhiteSpace(actorId))
            throw new ArgumentException("ActorId is required.", nameof(actorId));

        foreach (var domainEvent in domainEvents)
        {
            await PublishAsync(domainEvent, actorId, cancellationToken).ConfigureAwait(false);
        }
    }

    private Task PublishAsync(IDomainEvent domainEvent, string actorId, CancellationToken cancellationToken) =>
        domainEvent switch
        {
            DeviceRegisteredDomainEvent e => Publish("DeviceRegistered", e.DeviceId, e.HomeId, actorId,
                new DeviceRegisteredPayload(e.DeviceId, e.HomeId), cancellationToken),
            DeviceStateChangedDomainEvent e => Publish("DeviceStateChanged", e.DeviceId, e.HomeId, actorId,
                new DeviceStateChangedPayload(e.DeviceId, e.HomeId, e.Property, e.OldValue, e.NewValue, e.Unit), cancellationToken),
            DeviceRemovedDomainEvent e => Publish("DeviceRemoved", e.DeviceId, e.HomeId, actorId,
                new DeviceRemovedPayload(e.DeviceId, e.HomeId), cancellationToken),
            _ => throw new NotSupportedException($"Unknown domain event type: {domainEvent.GetType().Name}"),
        };

    private Task Publish<TPayload>(
        string eventType,
        string deviceId,
        string homeId,
        string actorId,
        TPayload payload,
        CancellationToken cancellationToken)
    {
        var envelope = EventEnvelope<TPayload>.Create(
            eventType: eventType,
            aggregateId: deviceId,
            aggregateType: "Device",
            actorId: actorId,
            homeId: homeId,
            payload: payload);

        return _eventPublisher.PublishAsync(TopicName, envelope, cancellationToken);
    }
}
