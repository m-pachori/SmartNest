using Newtonsoft.Json;

namespace SmartNest.PlatformService.Persistence.Alert;

/// <summary>
/// Cosmos DB persistence model for the <c>alerts</c> container (partition key
/// <c>/homeId</c>, already provisioned in Task 1).
/// </summary>
internal sealed class AlertDocument
{
    [JsonProperty("id")]
    public string Id { get; set; } = default!;

    [JsonProperty("homeId")]
    public string HomeId { get; set; } = default!;

    public string DeviceId { get; set; } = default!;

    /// <summary>Serialized name of <c>AlertSeverity</c> (e.g. "Warning").</summary>
    public string Severity { get; set; } = default!;

    public string Message { get; set; } = default!;

    public bool Acknowledged { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? AcknowledgedAt { get; set; }
}
