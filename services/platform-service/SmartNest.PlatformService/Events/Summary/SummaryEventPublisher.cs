using SmartNest.Shared.Events;

namespace SmartNest.PlatformService.Events.Summary;

/// <summary>Publishes <c>SummaryGenerated</c> to the <c>home-events</c> topic (Task 8) - Audit already subscribes there.</summary>
public sealed class SummaryEventPublisher
{
    public const string TopicName = "home-events";

    private readonly IEventPublisher _eventPublisher;

    public SummaryEventPublisher(IEventPublisher eventPublisher)
    {
        _eventPublisher = eventPublisher ?? throw new ArgumentNullException(nameof(eventPublisher));
    }

    public Task PublishGeneratedAsync(string homeId, string date, int totalEvents, CancellationToken cancellationToken = default)
    {
        var envelope = EventEnvelope<SummaryGeneratedPayload>.Create(
            eventType: "SummaryGenerated",
            aggregateId: homeId,
            aggregateType: "Summary",
            actorId: "system:summary-service",
            homeId: homeId,
            payload: new SummaryGeneratedPayload(homeId, date, totalEvents));

        return _eventPublisher.PublishAsync(TopicName, envelope, cancellationToken);
    }
}
