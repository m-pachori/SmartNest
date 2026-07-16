namespace SmartNest.PlatformService.Events.Automation;

public sealed record AutomationExecutedPayload(string RuleId, string HomeId, string DeviceId, string ActionType);
