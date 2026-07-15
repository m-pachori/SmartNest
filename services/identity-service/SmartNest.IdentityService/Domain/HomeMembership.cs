using SmartNest.IdentityService.Domain.Events;
using SmartNest.IdentityService.Domain.ValueObjects;

namespace SmartNest.IdentityService.Domain;

/// <summary>
/// HomeMembership aggregate root - records that a given Entra ID user (<see cref="UserId"/>,
/// the token's `oid`) has been assigned a role scoped to a specific home. This is
/// smartnest-plan.md Task 4's "User aggregate" - named <c>HomeMembership</c> here rather
/// than <c>User</c> because Entra ID (not this service) is the system of record for the
/// user's actual identity/credentials; this aggregate only ever stores the per-home
/// role-assignment record. Only publishes the three documented domain events:
/// UserInvited, RoleAssigned, UserDeactivated.
/// </summary>
public sealed class HomeMembership
{
    private readonly List<IDomainEvent> _domainEvents = new();

    public string MembershipId { get; private set; } = default!;

    public string HomeId { get; private set; } = default!;

    public string UserId { get; private set; } = default!;

    public RoleAssignment CurrentAssignment { get; private set; } = default!;

    public MembershipStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    // For repository/document mapping only.
    private HomeMembership()
    {
    }

    public static HomeMembership Invite(string homeId, string userId, string role, string invitedByUserId)
    {
        if (string.IsNullOrWhiteSpace(homeId))
            throw new ArgumentException("HomeId is required.", nameof(homeId));
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("UserId is required.", nameof(userId));

        var now = DateTimeOffset.UtcNow;
        var membership = new HomeMembership
        {
            MembershipId = Guid.NewGuid().ToString(),
            HomeId = homeId,
            UserId = userId,
            CurrentAssignment = new RoleAssignment(role, invitedByUserId, now),
            Status = MembershipStatus.Active,
            CreatedAt = now,
            UpdatedAt = now,
        };

        membership._domainEvents.Add(new UserInvitedDomainEvent(membership.MembershipId, homeId, userId, role));
        return membership;
    }

    /// <summary>Reconstructs an existing HomeMembership from storage without raising domain events.</summary>
    internal static HomeMembership Rehydrate(
        string membershipId,
        string homeId,
        string userId,
        RoleAssignment currentAssignment,
        MembershipStatus status,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt) =>
        new()
        {
            MembershipId = membershipId,
            HomeId = homeId,
            UserId = userId,
            CurrentAssignment = currentAssignment,
            Status = status,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
        };

    public void AssignRole(string role, string assignedByUserId)
    {
        if (string.IsNullOrWhiteSpace(role))
            throw new ArgumentException("Role is required.", nameof(role));

        var now = DateTimeOffset.UtcNow;
        CurrentAssignment = new RoleAssignment(role, assignedByUserId, now);
        UpdatedAt = now;

        _domainEvents.Add(new RoleAssignedDomainEvent(MembershipId, HomeId, UserId, role));
    }

    /// <summary>Soft-deletes the membership - never physically removed, so the Audit Service retains full history.</summary>
    public void Deactivate()
    {
        Status = MembershipStatus.Deactivated;
        UpdatedAt = DateTimeOffset.UtcNow;

        _domainEvents.Add(new UserDeactivatedDomainEvent(MembershipId, HomeId, UserId));
    }

    public void ClearDomainEvents() => _domainEvents.Clear();
}
