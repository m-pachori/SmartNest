using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using SmartNest.PlatformService.Handlers.Automation;

namespace SmartNest.PlatformService.Functions.Automation;

/// <summary>
/// Service Bus trigger on the <c>device-events</c> topic's <c>automation</c> subscription
/// (already provisioned in Task 1). Reacts to every <c>DeviceStateChanged</c> event.
/// </summary>
public sealed class EvaluateRules
{
    private readonly EvaluateRulesHandler _handler;
    private readonly ILogger<EvaluateRules> _logger;

    public EvaluateRules(EvaluateRulesHandler handler, ILogger<EvaluateRules> logger)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function("EvaluateRules")]
    public async Task Run(
        [ServiceBusTrigger("device-events", "automation", Connection = "ServiceBus:ConnectionString")] string messageBody,
        CancellationToken cancellationToken)
    {
        try
        {
            await _handler.HandleAsync(messageBody, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process device-events message in EvaluateRules.");
            throw;
        }
    }
}
