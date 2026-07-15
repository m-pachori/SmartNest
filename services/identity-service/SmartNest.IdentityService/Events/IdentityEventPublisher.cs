using SmartNest.IdentityService.Domain.Events;
using SmartNest.Shared.Events;

namespace SmartNest.IdentityService.Events;

/// <summary>
/// Translates Identity Service domain events into the shared <see cref="EventEnvelope{TPayload}"/>
/// and publishes them to the <c>user-events</c> Service Bus topic, using the least-privilege
/// send-only connection string (see infra/main.bicep - IdentityServiceSend authorization rule).
/// </summary>
public sealed class IdentityEventPublisher
{
    public const string TopicName = "user-events";

    private readonly IEventPublisher _eventPublisher;

    public IdentityEventPublisher(IEventPublisher eventPublisher)
    {
        _eventPublisher = eventPublisher ?? throw new ArgumentNullException(nameof(eventPublisher));
    }

    /// <summary>
    /// Publishes every domain event on <paramref name="domainEvents"/> to <c>user-events</c>,
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
            UserInvitedDomainEvent e => Publish("UserInvited", e.MembershipId, e.HomeId, actorId,
                new UserInvitedPayload(e.MembershipId, e.HomeId, e.UserId, e.Role), cancellationToken),
            RoleAssignedDomainEvent e => Publish("RoleAssigned", e.MembershipId, e.HomeId, actorId,
                new RoleAssignedPayload(e.MembershipId, e.HomeId, e.UserId, e.Role), cancellationToken),
            UserDeactivatedDomainEvent e => Publish("UserDeactivated", e.MembershipId, e.HomeId, actorId,
                new UserDeactivatedPayload(e.MembershipId, e.HomeId, e.UserId), cancellationToken),
            _ => throw new NotSupportedException($"Unknown domain event type: {domainEvent.GetType().Name}"),
        };

    private Task Publish<TPayload>(
        string eventType,
        string membershipId,
        string homeId,
        string actorId,
        TPayload payload,
        CancellationToken cancellationToken)
    {
        var envelope = EventEnvelope<TPayload>.Create(
            eventType: eventType,
            aggregateId: membershipId,
            aggregateType: "HomeMembership",
            actorId: actorId,
            homeId: homeId,
            payload: payload);

        return _eventPublisher.PublishAsync(TopicName, envelope, cancellationToken);
    }
}
