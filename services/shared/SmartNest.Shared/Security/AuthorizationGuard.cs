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
}
