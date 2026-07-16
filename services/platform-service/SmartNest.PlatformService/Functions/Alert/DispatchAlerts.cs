using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using SmartNest.PlatformService.Handlers.Alert;

namespace SmartNest.PlatformService.Functions.Alert;

/// <summary>
/// Service Bus trigger on the <c>device-events</c> topic's <c>alert</c> subscription
/// (already provisioned in Task 1). Reacts to every <c>DeviceStateChanged</c> event.
/// </summary>
public sealed class DispatchAlerts
{
    private readonly DispatchAlertsHandler _handler;
    private readonly ILogger<DispatchAlerts> _logger;

    public DispatchAlerts(DispatchAlertsHandler handler, ILogger<DispatchAlerts> logger)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function("DispatchAlerts")]
    public async Task Run(
        [ServiceBusTrigger("device-events", "alert", Connection = "ServiceBus:ConnectionString")] string messageBody,
        CancellationToken cancellationToken)
    {
        try
        {
            await _handler.HandleAsync(messageBody, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process device-events message in DispatchAlerts.");
            throw;
        }
    }
}
