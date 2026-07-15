namespace SmartNest.IdentityService.Domain.Events;

/// <summary>
/// Marker for domain events raised by aggregates in this bounded context. Handlers
/// translate these into the shared <c>EventEnvelope</c> and publish to the
/// <c>user-events</c> Service Bus topic (see smartnest-plan.md Task 4).
/// </summary>
public interface IDomainEvent
{
}

public sealed record UserInvitedDomainEvent(string MembershipId, string HomeId, string UserId, string Role) : IDomainEvent;

public sealed record RoleAssignedDomainEvent(string MembershipId, string HomeId, string UserId, string Role) : IDomainEvent;

public sealed record UserDeactivatedDomainEvent(string MembershipId, string HomeId, string UserId) : IDomainEvent;
