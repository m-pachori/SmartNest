using SmartNest.Shared.Events;

namespace SmartNest.PlatformService.Events.Automation;

/// <summary>
/// Publishes <c>AutomationExecuted</c> to the <c>device-events</c> topic (for the Audit
/// chain - see smartnest-plan.md Task 5) whenever a Rule's action fires.
/// </summary>
public sealed class AutomationEventPublisher
{
    public const string TopicName = "device-events";

    private readonly IEventPublisher _eventPublisher;

    public AutomationEventPublisher(IEventPublisher eventPublisher)
    {
        _eventPublisher = eventPublisher ?? throw new ArgumentNullException(nameof(eventPublisher));
    }

    public Task PublishExecutedAsync(
        string ruleId,
        string homeId,
        string deviceId,
        string actionType,
        string actorId,
        CancellationToken cancellationToken = default)
    {
        var envelope = EventEnvelope<AutomationExecutedPayload>.Create(
            eventType: "AutomationExecuted",
            aggregateId: deviceId,
            aggregateType: "Automation",
            actorId: actorId,
            homeId: homeId,
            payload: new AutomationExecutedPayload(ruleId, homeId, deviceId, actionType));

        return _eventPublisher.PublishAsync(TopicName, envelope, cancellationToken);
    }
}
