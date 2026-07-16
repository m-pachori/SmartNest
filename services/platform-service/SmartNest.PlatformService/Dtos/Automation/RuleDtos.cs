using SmartNest.PlatformService.Domain.Automation.ValueObjects;

namespace SmartNest.PlatformService.Dtos.Automation;

public sealed record ConditionRequest(string Field, ConditionOperator Operator, string Value);

public sealed record RuleActionRequest(
    RuleActionType Type,
    string? TargetDeviceId,
    string? TargetProperty,
    string? TargetValue,
    string? AlertSeverity,
    string? AlertMessage);

public sealed record CreateRuleRequest(string? DeviceId, string Name, ConditionRequest Condition, RuleActionRequest Action);

public sealed record UpdateRuleRequest(string Name, ConditionRequest Condition, RuleActionRequest Action, bool Enabled);

public sealed record RuleResponse(
    string RuleId,
    string HomeId,
    string? DeviceId,
    string Name,
    ConditionRequest Condition,
    RuleActionRequest Action,
    bool Enabled,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static RuleResponse FromDomain(Domain.Automation.Rule rule) => new(
        RuleId: rule.RuleId,
        HomeId: rule.HomeId,
        DeviceId: rule.DeviceId,
        Name: rule.Name,
        Condition: new ConditionRequest(rule.Condition.Field, rule.Condition.Operator, rule.Condition.Value),
        Action: new RuleActionRequest(
            rule.Action.Type,
            rule.Action.TargetDeviceId,
            rule.Action.TargetProperty,
            rule.Action.TargetValue,
            rule.Action.AlertSeverity,
            rule.Action.AlertMessage),
        Enabled: rule.Enabled,
        CreatedAt: rule.CreatedAt,
        UpdatedAt: rule.UpdatedAt);
}

/// <summary>Maps wire-format request shapes to domain value objects.</summary>
public static class AutomationRequestExtensions
{
    public static Condition ToDomain(this ConditionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new Condition(request.Field, request.Operator, request.Value);
    }

    public static RuleAction ToDomain(this RuleActionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Type switch
        {
            RuleActionType.ChangeDeviceState => RuleAction.ChangeDeviceState(
                request.TargetDeviceId ?? throw new ArgumentException("TargetDeviceId is required for ChangeDeviceState actions."),
                request.TargetProperty ?? throw new ArgumentException("TargetProperty is required for ChangeDeviceState actions."),
                request.TargetValue ?? throw new ArgumentException("TargetValue is required for ChangeDeviceState actions.")),
            RuleActionType.RaiseAlert => RuleAction.RaiseAlert(
                request.AlertSeverity ?? throw new ArgumentException("AlertSeverity is required for RaiseAlert actions."),
                request.AlertMessage ?? throw new ArgumentException("AlertMessage is required for RaiseAlert actions.")),
            _ => throw new ArgumentException($"Unknown RuleActionType: {request.Type}"),
        };
    }
}
