using Newtonsoft.Json;

namespace SmartNest.PlatformService.Persistence.Media;

/// <summary>
/// Cosmos DB persistence model for the <c>media-metadata</c> container (partition key
/// <c>/homeId</c>, already provisioned in Task 1).
/// </summary>
public sealed class MediaMetadataDocument
{
    [JsonProperty("id")]
    public string Id { get; set; } = default!;

    [JsonProperty("homeId")]
    public string HomeId { get; set; } = default!;

    public string DeviceId { get; set; } = default!;

    /// <summary>Blob path relative to the container, e.g. <c>{deviceId}/{guid}.{ext}</c>. Used for idempotency checks.</summary>
    public string BlobName { get; set; } = default!;

    public string ContentType { get; set; } = default!;

    public long SizeBytes { get; set; }

    public DateTimeOffset UploadedAt { get; set; }

    public DateTimeOffset ProcessedAt { get; set; }
}
