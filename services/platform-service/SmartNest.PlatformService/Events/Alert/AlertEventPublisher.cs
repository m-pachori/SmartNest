using SmartNest.Shared.Events;

namespace SmartNest.PlatformService.Events.Alert;

/// <summary>
/// Publishes <c>AlertRaised</c>/<c>AlertDelivered</c> to the <c>device-events</c> topic
/// for Audit consumption (Task 6).
/// </summary>
public sealed class AlertEventPublisher
{
    public const string TopicName = "device-events";

    private readonly IEventPublisher _eventPublisher;

    public AlertEventPublisher(IEventPublisher eventPublisher)
    {
        _eventPublisher = eventPublisher ?? throw new ArgumentNullException(nameof(eventPublisher));
    }

    public Task PublishRaisedAsync(Domain.Alert.Alert alert, string actorId, CancellationToken cancellationToken = default)
    {
        var envelope = EventEnvelope<AlertRaisedPayload>.Create(
            eventType: "AlertRaised",
            aggregateId: alert.DeviceId,
            aggregateType: "Alert",
            actorId: actorId,
            homeId: alert.HomeId,
            payload: new AlertRaisedPayload(alert.AlertId, alert.HomeId, alert.DeviceId, alert.Severity.ToString(), alert.Message));

        return _eventPublisher.PublishAsync(TopicName, envelope, cancellationToken);
    }

    public Task PublishDeliveredAsync(Domain.Alert.Alert alert, string actorId, CancellationToken cancellationToken = default)
    {
        var envelope = EventEnvelope<AlertDeliveredPayload>.Create(
            eventType: "AlertDelivered",
            aggregateId: alert.DeviceId,
            aggregateType: "Alert",
            actorId: actorId,
            homeId: alert.HomeId,
            payload: new AlertDeliveredPayload(alert.AlertId, alert.HomeId));

        return _eventPublisher.PublishAsync(TopicName, envelope, cancellationToken);
    }
}
