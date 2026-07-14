namespace SmartNest.HomeService.Events;

public sealed record HomeCreatedPayload(string HomeId);

public sealed record RoomAddedPayload(string HomeId, string RoomId);

public sealed record HomeDeletedPayload(string HomeId);
