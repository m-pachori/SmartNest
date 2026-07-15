using SmartNest.IdentityService.Domain;
using SmartNest.IdentityService.Domain.ValueObjects;

namespace SmartNest.IdentityService.Persistence;

internal static class HomeMembershipDocumentMapper
{
    public static HomeMembershipDocument ToDocument(this HomeMembership membership)
    {
        ArgumentNullException.ThrowIfNull(membership);

        return new HomeMembershipDocument
        {
            Id = membership.MembershipId,
            HomeId = membership.HomeId,
            UserId = membership.UserId,
            Role = membership.CurrentAssignment.Role,
            AssignedByUserId = membership.CurrentAssignment.AssignedByUserId,
            AssignedAt = membership.CurrentAssignment.AssignedAt,
            Status = membership.Status.ToString(),
            CreatedAt = membership.CreatedAt,
            UpdatedAt = membership.UpdatedAt,
        };
    }

    public static HomeMembership ToDomain(this HomeMembershipDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var assignment = new RoleAssignment(document.Role, document.AssignedByUserId, document.AssignedAt);

        return HomeMembership.Rehydrate(
            document.Id,
            document.HomeId,
            document.UserId,
            assignment,
            Enum.Parse<MembershipStatus>(document.Status),
            document.CreatedAt,
            document.UpdatedAt);
    }
}
