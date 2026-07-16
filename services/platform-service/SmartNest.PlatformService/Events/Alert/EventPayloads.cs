namespace SmartNest.PlatformService.Events.Alert;

public sealed record AlertRaisedPayload(string AlertId, string HomeId, string DeviceId, string Severity, string Message);

public sealed record AlertDeliveredPayload(string AlertId, string HomeId);
