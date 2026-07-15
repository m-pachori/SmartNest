namespace SmartNest.Shared.Security;

/// <summary>
/// Static helpers enforcing the platform's RBAC rules: App Role membership and
/// <c>homeId</c> claim scoping (see smartnest-plan.md "RBAC Roles" section).
/// </summary>
public static class AuthorizationGuard
{
    /// <summary>
    /// Throws <see cref="ForbiddenException"/> unless <paramref name="user"/> has at
    /// least one of <paramref name="allowedRoles"/>.
    /// </summary>
    public static void RequireRole(CurrentUser user, params string[] allowedRoles)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (allowedRoles is null || allowedRoles.Length == 0)
            throw new ArgumentException("At least one allowed role must be specified.", nameof(allowedRoles));

        if (!allowedRoles.Any(user.HasRole))
        {
            throw new ForbiddenException(
                $"Caller does not have any of the required roles: {string.Join(", ", allowedRoles)}.");
        }
    }

    /// <summary>
    /// Throws <see cref="ForbiddenException"/> unless the caller's <c>homeId</c> claim
    /// matches <paramref name="resourceHomeId"/>.
    /// </summary>
    /// <remarks>
    /// Prefer <see cref="RequireOwnership"/> where possible: the <c>homeId</c> optional
    /// claim is not currently populated by Entra ID for this platform (no claim source is
    /// configured - see infra/setup-entra.ps1), so this check should only be used once a
    /// reliable claim source exists (Task 4 - Identity/Access Service).
    /// </remarks>
    public static void RequireHomeIdMatch(CurrentUser user, string resourceHomeId)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (string.IsNullOrWhiteSpace(resourceHomeId))
            throw new ArgumentException("Resource homeId is required.", nameof(resourceHomeId));

        if (string.IsNullOrWhiteSpace(user.HomeId) ||
            !string.Equals(user.HomeId, resourceHomeId, StringComparison.OrdinalIgnoreCase))
        {
            throw new ForbiddenException("Caller's homeId claim does not match the requested resource's homeId.");
        }
    }

    /// <summary>
    /// Throws <see cref="ForbiddenException"/> unless the caller (identified by the
    /// token's subject/object-id claim) is the owner of the resource, as recorded in
    /// storage. Use this instead of trusting a self-asserted <c>homeId</c> claim from the
    /// token: the caller cannot forge <paramref name="resourceOwnerId"/> since it comes
    /// from the loaded Cosmos DB document, not the request.
    /// </summary>
    public static void RequireOwnership(CurrentUser user, string resourceOwnerId)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (string.IsNullOrWhiteSpace(resourceOwnerId))
            throw new ArgumentException("Resource ownerId is required.", nameof(resourceOwnerId));

        if (!string.Equals(user.UserId, resourceOwnerId, StringComparison.OrdinalIgnoreCase))
        {
            throw new ForbiddenException("Caller does not own this resource.");
        }
    }
}
