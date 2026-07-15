namespace SmartNest.IdentityService.Domain.ValueObjects;

/// <summary>Lifecycle state of a <see cref="HomeMembership"/>.</summary>
public enum MembershipStatus
{
    Active,
    Deactivated,
}

/// <summary>
/// The role currently assigned to a member, and who assigned it - matches the App Role
/// values Entra ID issues in the JWT `roles` claim (SmartNest.Owner/Technician/Guest).
/// </summary>
public sealed record RoleAssignment(string Role, string AssignedByUserId, DateTimeOffset AssignedAt)
{
    public string Role { get; init; } = Require(Role, nameof(Role));

    public string AssignedByUserId { get; init; } = Require(AssignedByUserId, nameof(AssignedByUserId));

    private static string Require(string value, string paramName) =>
        !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException($"{paramName} is required.", paramName);
}
