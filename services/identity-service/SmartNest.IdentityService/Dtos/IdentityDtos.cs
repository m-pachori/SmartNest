using SmartNest.IdentityService.Domain;

namespace SmartNest.IdentityService.Dtos;

public sealed record InviteUserRequest(string TargetUserId, string Role);

public sealed record UpdateUserRoleRequest(string Role);

public sealed record MembershipResponse(
    string MembershipId,
    string HomeId,
    string UserId,
    string Role,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static MembershipResponse FromDomain(HomeMembership membership) => new(
        MembershipId: membership.MembershipId,
        HomeId: membership.HomeId,
        UserId: membership.UserId,
        Role: membership.CurrentAssignment.Role,
        Status: membership.Status.ToString(),
        CreatedAt: membership.CreatedAt,
        UpdatedAt: membership.UpdatedAt);
}
