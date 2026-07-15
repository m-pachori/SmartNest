using SmartNest.HomeService.Domain.Events;
using SmartNest.Shared.Events;

namespace SmartNest.HomeService.Events;

/// <summary>
/// Translates Home Service domain events into the shared <see cref="EventEnvelope{TPayload}"/>
/// and publishes them to the <c>home-events</c> Service Bus topic.
/// </summary>
public sealed class HomeEventPublisher
{
    public const string TopicName = "home-events";

    private readonly IEventPublisher _eventPublisher;

    public HomeEventPublisher(IEventPublisher eventPublisher)
    {
        _eventPublisher = eventPublisher ?? throw new ArgumentNullException(nameof(eventPublisher));
    }

    /// <summary>
    /// Publishes every domain event on <paramref name="domainEvents"/> to <c>home-events</c>,
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
            HomeCreatedDomainEvent e => Publish("HomeCreated", e.HomeId, actorId,
                new HomeCreatedPayload(e.HomeId), cancellationToken),
            RoomAddedDomainEvent e => Publish("RoomAdded", e.HomeId, actorId,
                new RoomAddedPayload(e.HomeId, e.RoomId), cancellationToken),
            HomeDeletedDomainEvent e => Publish("HomeDeleted", e.HomeId, actorId,
                new HomeDeletedPayload(e.HomeId), cancellationToken),
            _ => throw new NotSupportedException($"Unknown domain event type: {domainEvent.GetType().Name}"),
        };

    private Task Publish<TPayload>(
        string eventType,
        string homeId,
        string actorId,
        TPayload payload,
        CancellationToken cancellationToken)
    {
        var envelope = EventEnvelope<TPayload>.Create(
            eventType: eventType,
            aggregateId: homeId,
            aggregateType: "Home",
            actorId: actorId,
            homeId: homeId,
            payload: payload);

        return _eventPublisher.PublishAsync(TopicName, envelope, cancellationToken);
    }
}
