using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using SmartNest.PlatformService.Handlers.Audit;

namespace SmartNest.PlatformService.Functions.Audit;

/// <summary>Service Bus trigger on the <c>home-events</c> topic's <c>audit</c> subscription (already provisioned in Task 1).</summary>
public sealed class AppendHomeEvents
{
    private readonly AppendAuditLogHandler _handler;
    private readonly ILogger<AppendHomeEvents> _logger;

    public AppendHomeEvents(AppendAuditLogHandler handler, ILogger<AppendHomeEvents> logger)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function("AppendHomeEvents")]
    public async Task Run(
        [ServiceBusTrigger("home-events", "audit", Connection = "ServiceBus:ConnectionString")] string messageBody,
        CancellationToken cancellationToken)
    {
        try
        {
            await _handler.HandleAsync(messageBody, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to append home-events message to the audit log.");
            throw;
        }
    }
}
