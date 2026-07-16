using SmartNest.PlatformService.Domain.Alert.ValueObjects;

namespace SmartNest.PlatformService.Dtos.Alert;

public sealed record AlertResponse(
    string AlertId,
    string HomeId,
    string DeviceId,
    AlertSeverity Severity,
    string Message,
    bool Acknowledged,
    DateTimeOffset CreatedAt,
    DateTimeOffset? AcknowledgedAt)
{
    public static AlertResponse FromDomain(Domain.Alert.Alert alert) => new(
        AlertId: alert.AlertId,
        HomeId: alert.HomeId,
        DeviceId: alert.DeviceId,
        Severity: alert.Severity,
        Message: alert.Message,
        Acknowledged: alert.Acknowledged,
        CreatedAt: alert.CreatedAt,
        AcknowledgedAt: alert.AcknowledgedAt);
}
