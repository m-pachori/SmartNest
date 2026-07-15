namespace SmartNest.IdentityService.Events;

public sealed record UserInvitedPayload(string MembershipId, string HomeId, string UserId, string Role);

public sealed record RoleAssignedPayload(string MembershipId, string HomeId, string UserId, string Role);

public sealed record UserDeactivatedPayload(string MembershipId, string HomeId, string UserId);
