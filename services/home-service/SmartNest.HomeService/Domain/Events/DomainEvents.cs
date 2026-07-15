namespace SmartNest.HomeService.Domain.Events;

/// <summary>
/// Marker for domain events raised by aggregates in this bounded context. Handlers
/// translate these into the shared <c>EventEnvelope</c> and publish to the
/// <c>home-events</c> Service Bus topic (see smartnest-plan.md Task 2).
/// </summary>
public interface IDomainEvent
{
}

public sealed record HomeCreatedDomainEvent(string HomeId) : IDomainEvent;

public sealed record RoomAddedDomainEvent(string HomeId, string RoomId) : IDomainEvent;

public sealed record HomeDeletedDomainEvent(string HomeId) : IDomainEvent;
