using SmartNest.PlatformService.Dtos.Media;
using SmartNest.PlatformService.Infrastructure;

namespace SmartNest.PlatformService.Handlers.Media;

/// <summary>
/// Handles the upload half of Task 9's two-step trigger path: validates the file, writes
/// it to <c>media-uploads/{deviceId}/{guid}.{ext}</c>, and returns immediately - no
/// processing happens here. The Blob trigger (<see cref="ProcessMediaHandler"/>) does all
/// processing asynchronously once the write completes.
/// </summary>
public sealed class UploadMediaHandler
{
    private const long MaxSizeBytes = 10 * 1024 * 1024; // 10 MB, per smartnest-plan.md Task 9

    private static readonly IReadOnlyDictionary<string, string> AllowedContentTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = "jpg",
        ["image/png"] = "png",
        ["application/pdf"] = "pdf",
    };

    private readonly IBlobStorageClientFactory _blobStorageClientFactory;
    private readonly string _mediaUploadsContainerName;

    public UploadMediaHandler(IBlobStorageClientFactory blobStorageClientFactory, string mediaUploadsContainerName)
    {
        _blobStorageClientFactory = blobStorageClientFactory ?? throw new ArgumentNullException(nameof(blobStorageClientFactory));
        if (string.IsNullOrWhiteSpace(mediaUploadsContainerName))
            throw new ArgumentException("Media uploads container name is required.", nameof(mediaUploadsContainerName));

        _mediaUploadsContainerName = mediaUploadsContainerName;
    }

    /// <summary>Thrown by the caller (HTTP Function) as 413 when <paramref name="contentLength"/> exceeds <see cref="MaxSizeBytes"/>.</summary>
    public sealed class PayloadTooLargeException : Exception
    {
        public PayloadTooLargeException(string message) : base(message)
        {
        }
    }

    public async Task<UploadMediaResponse> HandleAsync(
        string deviceId,
        string contentType,
        long contentLength,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
            throw new ArgumentException("DeviceId is required.", nameof(deviceId));
        if (string.IsNullOrWhiteSpace(contentType) || !AllowedContentTypes.TryGetValue(contentType, out var extension))
            throw new ArgumentException($"Unsupported Content-Type '{contentType}'. Allowed: {string.Join(", ", AllowedContentTypes.Keys)}.");
        if (contentLength > MaxSizeBytes)
            throw new PayloadTooLargeException($"Upload size {contentLength} bytes exceeds the {MaxSizeBytes}-byte limit.");
        ArgumentNullException.ThrowIfNull(content);

        var blobName = $"{deviceId}/{Guid.NewGuid()}.{extension}";
        var container = _blobStorageClientFactory.GetContainerClient(_mediaUploadsContainerName);
        var blobClient = container.GetBlobClient(blobName);

        await blobClient.UploadAsync(content, overwrite: true, cancellationToken).ConfigureAwait(false);

        return new UploadMediaResponse(blobName, contentType, contentLength);
    }
}
