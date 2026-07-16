namespace SmartNest.PlatformService.Infrastructure;

/// <summary>
/// Internal service-to-service client used by Automation's "ChangeDeviceState" rule
/// action to call Device Service's existing HTTP endpoint (no direct repository coupling
/// across bounded contexts - Device Service remains a separately deployed Function App).
/// </summary>
public interface IDeviceStateClient
{
    Task UpdateStateAsync(string deviceId, string property, string value, CancellationToken cancellationToken = default);
}
