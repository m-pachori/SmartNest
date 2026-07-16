using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using SmartNest.PlatformService.Handlers.Summary;

namespace SmartNest.PlatformService.Functions.Summary;

/// <summary>Timer trigger (Task 8) - runs daily at midnight UTC.</summary>
public sealed class GenerateDailySummary
{
    private readonly GenerateDailySummaryHandler _handler;
    private readonly ILogger<GenerateDailySummary> _logger;

    public GenerateDailySummary(GenerateDailySummaryHandler handler, ILogger<GenerateDailySummary> logger)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function("GenerateDailySummary")]
    public async Task Run([TimerTrigger("0 0 0 * * *")] TimerInfo timer, CancellationToken cancellationToken)
    {
        _logger.LogInformation("GenerateDailySummary starting at {StartedAt}.", DateTimeOffset.UtcNow);
        await _handler.RunAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("GenerateDailySummary completed at {CompletedAt}.", DateTimeOffset.UtcNow);
    }
}
