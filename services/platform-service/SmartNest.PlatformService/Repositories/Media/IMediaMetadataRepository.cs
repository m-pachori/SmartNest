using SmartNest.PlatformService.Persistence.Media;

namespace SmartNest.PlatformService.Repositories.Media;

public interface IMediaMetadataRepository
{
    /// <summary>Used by ProcessMedia's idempotency check - true if this blob was already processed.</summary>
    Task<bool> ExistsByBlobNameAsync(string homeId, string blobName, CancellationToken cancellationToken = default);

    Task CreateAsync(MediaMetadataDocument document, CancellationToken cancellationToken = default);
}
