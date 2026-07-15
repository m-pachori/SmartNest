namespace SmartNest.DeviceService.Domain.Events;

/// <summary>
/// Marker for domain events raised by aggregates in this bounded context. Handlers
/// translate these into the shared <c>EventEnvelope</c> and publish to the
/// <c>device-events</c> Service Bus topic (see smartnest-plan.md Task 3).
/// </summary>
public interface IDomainEvent
{
}

public sealed record DeviceRegisteredDomainEvent(string DeviceId, string HomeId) : IDomainEvent;

public sealed record DeviceStateChangedDomainEvent(
    string DeviceId,
    string HomeId,
    string Property,
    string? OldValue,
    string NewValue,
    string? Unit) : IDomainEvent;

public sealed record DeviceRemovedDomainEvent(string DeviceId, string HomeId) : IDomainEvent;
