using Microsoft.Extensions.Logging;

namespace SmartNest.PlatformService.Infrastructure;

/// <summary>Stub <see cref="INotificationSender"/> - logs via <see cref="ILogger"/> instead of delivering a real notification.</summary>
public sealed class LoggingNotificationSender : INotificationSender
{
    private readonly ILogger<LoggingNotificationSender> _logger;

    public LoggingNotificationSender(ILogger<LoggingNotificationSender> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task SendAsync(string homeId, string message, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Notification stub for home {HomeId}: {Message}", homeId, message);
        return Task.CompletedTask;
    }
}
