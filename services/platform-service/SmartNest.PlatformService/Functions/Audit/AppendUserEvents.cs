using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using SmartNest.PlatformService.Handlers.Audit;

namespace SmartNest.PlatformService.Functions.Audit;

/// <summary>Service Bus trigger on the <c>user-events</c> topic's <c>audit</c> subscription (already provisioned in Task 1).</summary>
public sealed class AppendUserEvents
{
    private readonly AppendAuditLogHandler _handler;
    private readonly ILogger<AppendUserEvents> _logger;

    public AppendUserEvents(AppendAuditLogHandler handler, ILogger<AppendUserEvents> logger)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function("AppendUserEvents")]
    public async Task Run(
        [ServiceBusTrigger("user-events", "audit", Connection = "ServiceBus:ConnectionString")] string messageBody,
        CancellationToken cancellationToken)
    {
        try
        {
            await _handler.HandleAsync(messageBody, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to append user-events message to the audit log.");
            throw;
        }
    }
}
