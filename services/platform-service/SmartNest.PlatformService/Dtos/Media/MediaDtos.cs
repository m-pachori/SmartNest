namespace SmartNest.PlatformService.Dtos.Media;

public sealed record UploadMediaResponse(string BlobName, string ContentType, long SizeBytes);
