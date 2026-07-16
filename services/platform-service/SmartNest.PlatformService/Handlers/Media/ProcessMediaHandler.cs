using SmartNest.PlatformService.Events.Media;
using SmartNest.PlatformService.Infrastructure;
using SmartNest.PlatformService.Persistence.Media;
using SmartNest.PlatformService.Repositories.Media;
using SmartNest.PlatformService.Repositories.Shared;

namespace SmartNest.PlatformService.Handlers.Media;

/// <summary>
/// Handles the processing half of Task 9's two-step trigger path (Blob trigger on
/// <c>media-uploads/{name}</c>): idempotency check, copy to <c>processed-media</c>,
/// delete the source blob, persist <see cref="MediaMetadataDocument"/>, publish
/// <c>DocumentProcessed</c>.
/// </summary>
public sealed class ProcessMediaHandler
{
    private readonly IBlobStorageClientFactory _blobStorageClientFactory;
    private readonly IMediaMetadataRepository _repository;
    private readonly IDeviceHomeLookupRepository _deviceHomeLookupRepository;
    private readonly MediaEventPublisher _eventPublisher;
    private readonly string _mediaUploadsContainerName;
    private readonly string _processedMediaContainerName;

    public ProcessMediaHandler(
        IBlobStorageClientFactory blobStorageClientFactory,
        IMediaMetadataRepository repository,
        IDeviceHomeLookupRepository deviceHomeLookupRepository,
        MediaEventPublisher eventPublisher,
        string mediaUploadsContainerName,
        string processedMediaContainerName)
    {
        _blobStorageClientFactory = blobStorageClientFactory ?? throw new ArgumentNullException(nameof(blobStorageClientFactory));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _deviceHomeLookupRepository = deviceHomeLookupRepository ?? throw new ArgumentNullException(nameof(deviceHomeLookupRepository));
        _eventPublisher = eventPublisher ?? throw new ArgumentNullException(nameof(eventPublisher));
        _mediaUploadsContainerName = mediaUploadsContainerName;
        _processedMediaContainerName = processedMediaContainerName;
    }

    public async Task HandleAsync(string blobName, Stream content, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(blobName))
            throw new ArgumentException("BlobName is required.", nameof(blobName));
        ArgumentNullException.ThrowIfNull(content);

        var segments = blobName.Split('/', 2);
        if (segments.Length != 2)
            throw new ArgumentException($"Blob name '{blobName}' does not match the expected '{{deviceId}}/{{fileName}}' shape.");

        var deviceId = segments[0];

        var homeId = await _deviceHomeLookupRepository.GetHomeIdAsync(deviceId, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Device '{deviceId}' was not found.");

        // Idempotency: Blob triggers are at-least-once, so a redelivery must be a no-op.
        if (await _repository.ExistsByBlobNameAsync(homeId, blobName, cancellationToken).ConfigureAwait(false))
            return;

        var sourceContainer = _blobStorageClientFactory.GetContainerClient(_mediaUploadsContainerName);
        var sourceBlob = sourceContainer.GetBlobClient(blobName);
        var properties = await sourceBlob.GetPropertiesAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        var contentType = properties.Value.ContentType;
        var sizeBytes = properties.Value.ContentLength;

        var destinationContainer = _blobStorageClientFactory.GetContainerClient(_processedMediaContainerName);
        var destinationBlob = destinationContainer.GetBlobClient(blobName);
        await destinationBlob.UploadAsync(content, overwrite: true, cancellationToken).ConfigureAwait(false);

        await sourceBlob.DeleteIfExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

        var metadata = new MediaMetadataDocument
        {
            Id = Guid.NewGuid().ToString(),
            HomeId = homeId,
            DeviceId = deviceId,
            BlobName = blobName,
            ContentType = contentType,
            SizeBytes = sizeBytes,
            UploadedAt = properties.Value.CreatedOn,
            ProcessedAt = DateTimeOffset.UtcNow,
        };
        await _repository.CreateAsync(metadata, cancellationToken).ConfigureAwait(false);

        await _eventPublisher.PublishProcessedAsync(homeId, deviceId, blobName, contentType, sizeBytes, cancellationToken).ConfigureAwait(false);
    }
}
