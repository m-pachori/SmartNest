using SmartNest.IdentityService.Domain;

namespace SmartNest.IdentityService.Repositories;

public interface IIdentityRepository
{
    /// <summary>
    /// Cross-partition lookup by membershipId alone. The flat <c>PUT /users/{id}/role</c>
    /// route doesn't carry the <c>homeId</c> partition key, so this cannot be a direct
    /// point-read (mirrors <c>IDeviceRepository.GetAsync</c> in Device Service).
    /// </summary>
    Task<HomeMembership?> GetAsync(string membershipId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Partition-scoped lookup (query, not point-read, since the document id is a
    /// generated membershipId, not the userId). Used for invite deduplication and by
    /// <c>RemoveUser</c>, whose route carries <c>homeId</c> directly.
    /// </summary>
    Task<HomeMembership?> GetByHomeAndUserAsync(string homeId, string userId, CancellationToken cancellationToken = default);

    Task CreateAsync(HomeMembership membership, CancellationToken cancellationToken = default);

    Task UpdateAsync(HomeMembership membership, CancellationToken cancellationToken = default);
}
