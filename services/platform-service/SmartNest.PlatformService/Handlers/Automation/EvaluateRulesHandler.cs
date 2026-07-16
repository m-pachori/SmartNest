using System.Text.Json;
using SmartNest.PlatformService.Domain.Automation.ValueObjects;
using SmartNest.PlatformService.Events.Automation;
using SmartNest.PlatformService.Events.Shared;
using SmartNest.PlatformService.Handlers.Alert;
using SmartNest.PlatformService.Infrastructure;
using SmartNest.PlatformService.Repositories.Automation;
using SmartNest.Shared.Events;

namespace SmartNest.PlatformService.Handlers.Automation;

/// <summary>
/// Service Bus trigger handler (Task 5) - consumes <c>DeviceStateChanged</c> from the
/// <c>device-events</c> topic's <c>automation</c> subscription (already provisioned in
/// Task 1). Loads enabled rules for the event's home, evaluates each rule's
/// <see cref="Condition"/>, and executes the matching rule's <see cref="RuleAction"/>:
/// - ChangeDeviceState calls Device Service's HTTP endpoint via <see cref="IDeviceStateClient"/>.
/// - RaiseAlert calls <see cref="CreateAlertHandler"/> in-process (same deployment - no
///   Service Bus round-trip, see plan-platformService.prompt.md Decisions).
/// Publishes <c>AutomationExecuted</c> for every rule that fires.
/// </summary>
public sealed class EvaluateRulesHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IRuleRepository _ruleRepository;
    private readonly IDeviceStateClient _deviceStateClient;
    private readonly CreateAlertHandler _createAlertHandler;
    private readonly AutomationEventPublisher _eventPublisher;

    public EvaluateRulesHandler(
        IRuleRepository ruleRepository,
        IDeviceStateClient deviceStateClient,
        CreateAlertHandler createAlertHandler,
        AutomationEventPublisher eventPublisher)
    {
        _ruleRepository = ruleRepository ?? throw new ArgumentNullException(nameof(ruleRepository));
        _deviceStateClient = deviceStateClient ?? throw new ArgumentNullException(nameof(deviceStateClient));
        _createAlertHandler = createAlertHandler ?? throw new ArgumentNullException(nameof(createAlertHandler));
        _eventPublisher = eventPublisher ?? throw new ArgumentNullException(nameof(eventPublisher));
    }

    public async Task HandleAsync(string messageBody, CancellationToken cancellationToken = default)
    {
        var envelope = JsonSerializer.Deserialize<EventEnvelope<DeviceStateChangedPayload>>(messageBody, JsonOptions);
        if (envelope is null || !string.Equals(envelope.EventType, "DeviceStateChanged", StringComparison.OrdinalIgnoreCase))
            return;

        var payload = envelope.Payload;
        // System.Text.Json deserialization bypasses EventEnvelope<T>.Create()'s null check,
        // so a malformed/incomplete message body (e.g. a missing "payload" property) can
        // still produce a null Payload here - guard explicitly rather than letting a
        // NullReferenceException poison redelivery of this message.
        if (payload is null || string.IsNullOrWhiteSpace(payload.HomeId) || string.IsNullOrWhiteSpace(payload.DeviceId))
            return;

        var rules = await _ruleRepository.GetEnabledByHomeIdAsync(payload.HomeId, cancellationToken).ConfigureAwait(false);

        foreach (var rule in rules)
        {
            if (!rule.AppliesTo(payload.DeviceId))
                continue;
            if (!rule.Condition.Matches(payload.Property, payload.NewValue))
                continue;

            await ExecuteActionAsync(rule, payload, cancellationToken).ConfigureAwait(false);

            await _eventPublisher
                .PublishExecutedAsync(rule.RuleId, payload.HomeId, payload.DeviceId, rule.Action.Type.ToString(), envelope.ActorId, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private async Task ExecuteActionAsync(Domain.Automation.Rule rule, DeviceStateChangedPayload payload, CancellationToken cancellationToken)
    {
        switch (rule.Action.Type)
        {
            case RuleActionType.ChangeDeviceState:
                await _deviceStateClient
                    .UpdateStateAsync(rule.Action.TargetDeviceId!, rule.Action.TargetProperty!, rule.Action.TargetValue!, cancellationToken)
                    .ConfigureAwait(false);
                break;

            case RuleActionType.RaiseAlert:
                await _createAlertHandler
                    .HandleAsync(
                        payload.HomeId,
                        payload.DeviceId,
                        Enum.Parse<Domain.Alert.ValueObjects.AlertSeverity>(rule.Action.AlertSeverity!),
                        rule.Action.AlertMessage!,
                        actorId: "system:automation-service",
                        cancellationToken)
                    .ConfigureAwait(false);
                break;

            default:
                throw new NotSupportedException($"Unknown RuleActionType: {rule.Action.Type}");
        }
    }
}
