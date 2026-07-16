using SmartNest.PlatformService.Domain.Automation.ValueObjects;

namespace SmartNest.PlatformService.Domain.Automation;

/// <summary>
/// Rule aggregate root (Automation bounded context - Task 5). Owner-authored condition +
/// action, optionally scoped to a specific device. Evaluated by
/// <c>Handlers.Automation.EvaluateRulesHandler</c> against every incoming
/// <c>DeviceStateChanged</c> event for the rule's home.
/// </summary>
public sealed class Rule
{
    public string RuleId { get; private set; } = default!;

    public string HomeId { get; private set; } = default!;

    public string? DeviceId { get; private set; }

    public string Name { get; private set; } = default!;

    public Condition Condition { get; private set; } = default!;

    public RuleAction Action { get; private set; } = default!;

    public bool Enabled { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    private Rule()
    {
    }

    public static Rule Create(string homeId, string? deviceId, string name, Condition condition, RuleAction action)
    {
        if (string.IsNullOrWhiteSpace(homeId))
            throw new ArgumentException("HomeId is required.", nameof(homeId));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Rule name is required.", nameof(name));
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(action);

        var now = DateTimeOffset.UtcNow;
        return new Rule
        {
            RuleId = Guid.NewGuid().ToString(),
            HomeId = homeId,
            DeviceId = deviceId,
            Name = name,
            Condition = condition,
            Action = action,
            Enabled = true,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    internal static Rule Rehydrate(
        string ruleId,
        string homeId,
        string? deviceId,
        string name,
        Condition condition,
        RuleAction action,
        bool enabled,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt) => new()
        {
            RuleId = ruleId,
            HomeId = homeId,
            DeviceId = deviceId,
            Name = name,
            Condition = condition,
            Action = action,
            Enabled = enabled,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
        };

    public void UpdateDetails(string name, Condition condition, RuleAction action, bool enabled)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Rule name is required.", nameof(name));
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(action);

        Name = name;
        Condition = condition;
        Action = action;
        Enabled = enabled;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Returns true if this rule applies to (and is scoped correctly for) the given device.</summary>
    public bool AppliesTo(string deviceId) =>
        Enabled && (DeviceId is null || string.Equals(DeviceId, deviceId, StringComparison.OrdinalIgnoreCase));
}
