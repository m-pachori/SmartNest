namespace SmartNest.PlatformService.Repositories.Shared;

/// <summary>
/// Read-only lookup against the (shared) <c>homes</c> Cosmos container - used to verify
/// that the caller performing a home-scoped operation actually owns the target home,
/// mirroring Home/Device/Identity Service's <c>AuthorizationGuard.RequireOwnership</c>
/// pattern rather than trusting the JWT's <c>homeId</c> claim. Reused by every bounded
/// context in this merged Function App that needs an ownership check (Automation, Alert).
/// </summary>
public interface IHomeOwnershipRepository
{
    /// <summary>Returns the home's OwnerId, or null if the home doesn't exist.</summary>
    Task<string?> GetOwnerIdAsync(string homeId, CancellationToken cancellationToken = default);
}
