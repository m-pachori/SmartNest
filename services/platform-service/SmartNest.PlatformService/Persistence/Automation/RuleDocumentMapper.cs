using SmartNest.PlatformService.Domain.Automation;
using SmartNest.PlatformService.Domain.Automation.ValueObjects;

namespace SmartNest.PlatformService.Persistence.Automation;

internal static class RuleDocumentMapper
{
    public static RuleDocument ToDocument(this Rule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);

        return new RuleDocument
        {
            Id = rule.RuleId,
            HomeId = rule.HomeId,
            DeviceId = rule.DeviceId,
            Name = rule.Name,
            Condition = new ConditionDocument
            {
                Field = rule.Condition.Field,
                Operator = rule.Condition.Operator.ToString(),
                Value = rule.Condition.Value,
            },
            Action = new RuleActionDocument
            {
                Type = rule.Action.Type.ToString(),
                TargetDeviceId = rule.Action.TargetDeviceId,
                TargetProperty = rule.Action.TargetProperty,
                TargetValue = rule.Action.TargetValue,
                AlertSeverity = rule.Action.AlertSeverity,
                AlertMessage = rule.Action.AlertMessage,
            },
            Enabled = rule.Enabled,
            CreatedAt = rule.CreatedAt,
            UpdatedAt = rule.UpdatedAt,
        };
    }

    public static Rule ToDomain(this RuleDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var condition = new Condition(
            document.Condition.Field,
            Enum.Parse<ConditionOperator>(document.Condition.Operator),
            document.Condition.Value);

        var action = new RuleAction(
            Enum.Parse<RuleActionType>(document.Action.Type),
            document.Action.TargetDeviceId,
            document.Action.TargetProperty,
            document.Action.TargetValue,
            document.Action.AlertSeverity,
            document.Action.AlertMessage);

        return Rule.Rehydrate(
            document.Id,
            document.HomeId,
            document.DeviceId,
            document.Name,
            condition,
            action,
            document.Enabled,
            document.CreatedAt,
            document.UpdatedAt);
    }
}
