namespace SmartNest.PlatformService.Infrastructure;

/// <summary>
/// Notification delivery abstraction for Alert Service. Stubbed per smartnest-plan.md
/// Task 6 ("Notification delivery ... can be stubbed") - swap
/// <see cref="LoggingNotificationSender"/> for a real channel (e.g. Azure Communication
/// Services) later without touching handler code.
/// </summary>
public interface INotificationSender
{
    Task SendAsync(string homeId, string message, CancellationToken cancellationToken = default);
}
