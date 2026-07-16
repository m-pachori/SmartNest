using Newtonsoft.Json;

namespace SmartNest.PlatformService.Persistence.Automation;

/// <summary>
/// Cosmos DB persistence model for the <c>rules</c> container (partition key
/// <c>/homeId</c>, already provisioned in Task 1 - see infra/modules/cosmos-db.bicep).
/// </summary>
internal sealed class RuleDocument
{
    [JsonProperty("id")]
    public string Id { get; set; } = default!;

    [JsonProperty("homeId")]
    public string HomeId { get; set; } = default!;

    public string? DeviceId { get; set; }

    public string Name { get; set; } = default!;

    public ConditionDocument Condition { get; set; } = default!;

    public RuleActionDocument Action { get; set; } = default!;

    public bool Enabled { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

internal sealed class ConditionDocument
{
    public string Field { get; set; } = default!;

    /// <summary>Serialized name of <c>ConditionOperator</c> (e.g. "GreaterThan").</summary>
    public string Operator { get; set; } = default!;

    public string Value { get; set; } = default!;
}

internal sealed class RuleActionDocument
{
    /// <summary>Serialized name of <c>RuleActionType</c> (e.g. "ChangeDeviceState").</summary>
    public string Type { get; set; } = default!;

    public string? TargetDeviceId { get; set; }

    public string? TargetProperty { get; set; }

    public string? TargetValue { get; set; }

    public string? AlertSeverity { get; set; }

    public string? AlertMessage { get; set; }
}
