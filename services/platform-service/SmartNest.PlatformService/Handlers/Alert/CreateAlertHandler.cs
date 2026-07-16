using SmartNest.PlatformService.Domain.Alert.ValueObjects;
using SmartNest.PlatformService.Events.Alert;
using SmartNest.PlatformService.Infrastructure;
using SmartNest.PlatformService.Repositories.Alert;

namespace SmartNest.PlatformService.Handlers.Alert;

/// <summary>
/// Creates and dispatches an Alert. This is the shared entry point reused by both
/// <see cref="DispatchAlertsHandler"/> (Service Bus trigger reacting to
/// <c>DeviceStateChanged</c>) and Automation's "RaiseAlert" rule action - called
/// in-process since both bounded contexts now live in the same Function App (no Service
/// Bus round-trip needed - see plan-platformService.prompt.md Decisions).
/// </summary>
public sealed class CreateAlertHandler
{
    private readonly IAlertRepository _repository;
    private readonly INotificationSender _notificationSender;
    private readonly AlertEventPublisher _eventPublisher;

    public CreateAlertHandler(IAlertRepository repository, INotificationSender notificationSender, AlertEventPublisher eventPublisher)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _notificationSender = notificationSender ?? throw new ArgumentNullException(nameof(notificationSender));
        _eventPublisher = eventPublisher ?? throw new ArgumentNullException(nameof(eventPublisher));
    }

    public async Task<Domain.Alert.Alert> HandleAsync(
        string homeId,
        string deviceId,
        AlertSeverity severity,
        string message,
        string actorId,
        CancellationToken cancellationToken = default)
    {
        var alert = Domain.Alert.Alert.Raise(homeId, deviceId, severity, message);

        await _repository.CreateAsync(alert, cancellationToken).ConfigureAwait(false);
        await _eventPublisher.PublishRaisedAsync(alert, actorId, cancellationToken).ConfigureAwait(false);

        await _notificationSender.SendAsync(homeId, message, cancellationToken).ConfigureAwait(false);
        await _eventPublisher.PublishDeliveredAsync(alert, actorId, cancellationToken).ConfigureAwait(false);

        return alert;
    }
}
