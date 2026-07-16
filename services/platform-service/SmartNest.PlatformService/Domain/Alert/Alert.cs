using SmartNest.PlatformService.Domain.Alert.ValueObjects;

namespace SmartNest.PlatformService.Domain.Alert;

/// <summary>
/// Alert aggregate root (Alert bounded context - Task 6). Raised either by
/// <c>DispatchAlertsHandler</c> (reacting directly to a <c>DeviceStateChanged</c> event)
/// or in-process by Automation's "RaiseAlert" rule action.
/// </summary>
public sealed class Alert
{
    public string AlertId { get; private set; } = default!;

    public string HomeId { get; private set; } = default!;

    public string DeviceId { get; private set; } = default!;

    public AlertSeverity Severity { get; private set; }

    public string Message { get; private set; } = default!;

    public bool Acknowledged { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? AcknowledgedAt { get; private set; }

    private Alert()
    {
    }

    public static Alert Raise(string homeId, string deviceId, AlertSeverity severity, string message)
    {
        if (string.IsNullOrWhiteSpace(homeId))
            throw new ArgumentException("HomeId is required.", nameof(homeId));
        if (string.IsNullOrWhiteSpace(deviceId))
            throw new ArgumentException("DeviceId is required.", nameof(deviceId));
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Message is required.", nameof(message));

        return new Alert
        {
            AlertId = Guid.NewGuid().ToString(),
            HomeId = homeId,
            DeviceId = deviceId,
            Severity = severity,
            Message = message,
            Acknowledged = false,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    internal static Alert Rehydrate(
        string alertId,
        string homeId,
        string deviceId,
        AlertSeverity severity,
        string message,
        bool acknowledged,
        DateTimeOffset createdAt,
        DateTimeOffset? acknowledgedAt) => new()
        {
            AlertId = alertId,
            HomeId = homeId,
            DeviceId = deviceId,
            Severity = severity,
            Message = message,
            Acknowledged = acknowledged,
            CreatedAt = createdAt,
            AcknowledgedAt = acknowledgedAt,
        };

    public void Acknowledge()
    {
        if (Acknowledged)
            return;

        Acknowledged = true;
        AcknowledgedAt = DateTimeOffset.UtcNow;
    }
}
