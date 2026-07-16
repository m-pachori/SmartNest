using SmartNest.PlatformService.Domain.Alert.ValueObjects;

namespace SmartNest.PlatformService.Persistence.Alert;

internal static class AlertDocumentMapper
{
    public static AlertDocument ToDocument(this Domain.Alert.Alert alert)
    {
        ArgumentNullException.ThrowIfNull(alert);

        return new AlertDocument
        {
            Id = alert.AlertId,
            HomeId = alert.HomeId,
            DeviceId = alert.DeviceId,
            Severity = alert.Severity.ToString(),
            Message = alert.Message,
            Acknowledged = alert.Acknowledged,
            CreatedAt = alert.CreatedAt,
            AcknowledgedAt = alert.AcknowledgedAt,
        };
    }

    public static Domain.Alert.Alert ToDomain(this AlertDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        return Domain.Alert.Alert.Rehydrate(
            document.Id,
            document.HomeId,
            document.DeviceId,
            Enum.Parse<AlertSeverity>(document.Severity),
            document.Message,
            document.Acknowledged,
            document.CreatedAt,
            document.AcknowledgedAt);
    }
}
