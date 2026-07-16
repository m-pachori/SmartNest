using System.Text.Json;
using SmartNest.PlatformService.Domain.Alert.ValueObjects;
using SmartNest.PlatformService.Events.Shared;
using SmartNest.Shared.Events;

namespace SmartNest.PlatformService.Handlers.Alert;

/// <summary>
/// Service Bus trigger handler (Task 6) - consumes <c>DeviceStateChanged</c> from the
/// <c>device-events</c> topic's <c>alert</c> subscription (already provisioned in Task 1).
/// Evaluates a simple built-in threshold (temperature &gt; 30) as the default alert
/// condition; a richer configurable-threshold model can be layered on later.
/// </summary>
public sealed class DispatchAlertsHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly CreateAlertHandler _createAlertHandler;

    public DispatchAlertsHandler(CreateAlertHandler createAlertHandler)
    {
        _createAlertHandler = createAlertHandler ?? throw new ArgumentNullException(nameof(createAlertHandler));
    }

    public async Task HandleAsync(string messageBody, CancellationToken cancellationToken = default)
    {
        var envelope = JsonSerializer.Deserialize<EventEnvelope<DeviceStateChangedPayload>>(messageBody, JsonOptions);
        if (envelope is null || !string.Equals(envelope.EventType, "DeviceStateChanged", StringComparison.OrdinalIgnoreCase))
            return;

        var payload = envelope.Payload;
        // Guard against a malformed/incomplete message body producing a null Payload -
        // see EvaluateRulesHandler for the same fix and rationale.
        if (payload is null || string.IsNullOrWhiteSpace(payload.HomeId) || string.IsNullOrWhiteSpace(payload.DeviceId))
            return;

        if (!IsAlertWorthy(payload, out var severity, out var message))
            return;

        await _createAlertHandler
            .HandleAsync(payload.HomeId, payload.DeviceId, severity, message, actorId: "system:alert-service", cancellationToken)
            .ConfigureAwait(false);
    }

    private static bool IsAlertWorthy(DeviceStateChangedPayload payload, out AlertSeverity severity, out string message)
    {
        if (string.Equals(payload.Property, "temperature", StringComparison.OrdinalIgnoreCase) &&
            double.TryParse(payload.NewValue, System.Globalization.CultureInfo.InvariantCulture, out var temperature) &&
            temperature > 30)
        {
            severity = AlertSeverity.Warning;
            message = $"Device {payload.DeviceId} reported temperature {payload.NewValue}{payload.Unit} (above 30 threshold).";
            return true;
        }

        severity = AlertSeverity.Info;
        message = string.Empty;
        return false;
    }
}
