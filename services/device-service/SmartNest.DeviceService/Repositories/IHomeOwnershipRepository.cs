namespace SmartNest.DeviceService.Repositories;

/// <summary>
/// Read-only lookup against the (shared) <c>homes</c> Cosmos container - used to verify
/// that the caller performing a home-scoped operation actually owns the target home,
/// mirroring Home Service's <c>AuthorizationGuard.RequireOwnership</c> pattern rather than
/// trusting the JWT's <c>homeId</c> claim (which Entra ID doesn't populate yet - see
/// plan-deviceService.prompt.md). This is the platform's interim ownership check until
/// Task 4 (Identity/Access Service) provides a real per-home role-assignment store for
/// Technician/Guest scoping.
/// </summary>
public interface IHomeOwnershipRepository
{
    /// <summary>Returns the home's OwnerId, or null if the home doesn't exist.</summary>
    Task<string?> GetOwnerIdAsync(string homeId, CancellationToken cancellationToken = default);
}
