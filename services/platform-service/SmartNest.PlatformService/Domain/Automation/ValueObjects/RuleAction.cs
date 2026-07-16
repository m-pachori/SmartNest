namespace SmartNest.PlatformService.Domain.Automation.ValueObjects;

/// <summary>What a <see cref="Domain.Automation.Rule"/> does when its <see cref="Condition"/> matches.</summary>
public enum RuleActionType
{
    ChangeDeviceState,
    RaiseAlert,
}

/// <summary>
/// The action a Rule performs when triggered. Exactly one action shape is populated
/// depending on <see cref="Type"/>:
/// - <see cref="ChangeDeviceState"/>: sets <see cref="TargetDeviceId"/>'s <see cref="TargetProperty"/> to <see cref="TargetValue"/>.
/// - <see cref="RaiseAlert"/>: raises an alert with <see cref="AlertSeverity"/>/<see cref="AlertMessage"/>.
/// </summary>
public sealed record RuleAction(
    RuleActionType Type,
    string? TargetDeviceId,
    string? TargetProperty,
    string? TargetValue,
    string? AlertSeverity,
    string? AlertMessage)
{
    public static RuleAction ChangeDeviceState(string targetDeviceId, string targetProperty, string targetValue)
    {
        if (string.IsNullOrWhiteSpace(targetDeviceId))
            throw new ArgumentException("TargetDeviceId is required.", nameof(targetDeviceId));
        if (string.IsNullOrWhiteSpace(targetProperty))
            throw new ArgumentException("TargetProperty is required.", nameof(targetProperty));
        if (string.IsNullOrWhiteSpace(targetValue))
            throw new ArgumentException("TargetValue is required.", nameof(targetValue));

        return new RuleAction(RuleActionType.ChangeDeviceState, targetDeviceId, targetProperty, targetValue, null, null);
    }

    public static RuleAction RaiseAlert(string severity, string message)
    {
        if (string.IsNullOrWhiteSpace(severity))
            throw new ArgumentException("Severity is required.", nameof(severity));
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Message is required.", nameof(message));

        return new RuleAction(RuleActionType.RaiseAlert, null, null, null, severity, message);
    }
}
