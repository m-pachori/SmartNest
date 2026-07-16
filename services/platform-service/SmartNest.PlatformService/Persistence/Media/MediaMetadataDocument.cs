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

    [JsonProperty("deviceId")]
    public string DeviceId { get; set; } = default!;

    /// <summary>Blob path relative to the container, e.g. <c>{deviceId}/{guid}.{ext}</c>. Used for idempotency checks.</summary>
    [JsonProperty("blobName")]
    public string BlobName { get; set; } = default!;

    [JsonProperty("contentType")]
    public string ContentType { get; set; } = default!;

    [JsonProperty("sizeBytes")]
    public long SizeBytes { get; set; }

    [JsonProperty("uploadedAt")]
    public DateTimeOffset UploadedAt { get; set; }

    [JsonProperty("processedAt")]
    public DateTimeOffset ProcessedAt { get; set; }
}
