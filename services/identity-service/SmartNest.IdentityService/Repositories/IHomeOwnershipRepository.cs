namespace SmartNest.IdentityService.Repositories;

/// <summary>
/// Read-only lookup against the (shared) <c>homes</c> Cosmos container - used to verify
/// that the caller managing membership actually owns the target home, mirroring Home
/// Service's <c>AuthorizationGuard.RequireOwnership</c> pattern (see Device Service's
/// identical copy and plan-identityService.prompt.md). Own copy per service - no shared
/// project reference between Function Apps.
/// </summary>
public interface IHomeOwnershipRepository
{
    /// <summary>Returns the home's OwnerId, or null if the home doesn't exist.</summary>
    Task<string?> GetOwnerIdAsync(string homeId, CancellationToken cancellationToken = default);
}
