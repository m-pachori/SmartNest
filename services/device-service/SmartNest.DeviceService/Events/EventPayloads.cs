namespace SmartNest.DeviceService.Events;

public sealed record DeviceRegisteredPayload(string DeviceId, string HomeId);

public sealed record DeviceStateChangedPayload(string DeviceId, string HomeId, string Property, string? OldValue, string NewValue, string? Unit);

public sealed record DeviceRemovedPayload(string DeviceId, string HomeId);
