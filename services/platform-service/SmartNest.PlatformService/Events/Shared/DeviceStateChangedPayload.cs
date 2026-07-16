namespace SmartNest.PlatformService.Events.Shared;

/// <summary>
/// Local copy of Device Service's <c>DeviceStateChangedPayload</c> shape - used to
/// deserialize incoming <c>device-events</c> Service Bus messages in the Automation and
/// Alert bounded contexts. Field names/casing must stay in sync with
/// SmartNest.DeviceService.Events.EventPayloads.DeviceStateChangedPayload.
/// </summary>
public sealed record DeviceStateChangedPayload(
    string DeviceId,
    string HomeId,
    string Property,
    string? OldValue,
    string NewValue,
    string? Unit);
