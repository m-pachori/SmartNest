namespace SmartNest.PlatformService.Events.Media;

public sealed record MediaProcessedPayload(string HomeId, string DeviceId, string BlobName, string ContentType, long SizeBytes);
