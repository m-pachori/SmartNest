using Newtonsoft.Json;

namespace SmartNest.DeviceService.Persistence;

/// <summary>
/// Cosmos DB persistence model for the <c>devices</c> container (partition key
/// <c>/homeId</c>). Kept separate from the <see cref="SmartNest.DeviceService.Domain.Device"/>
/// aggregate so the domain model stays persistence-ignorant.
/// </summary>
internal sealed class DeviceDocument
{
    [JsonProperty("id")]
    public string Id { get; set; } = default!;

    [JsonProperty("homeId")]
    public string HomeId { get; set; } = default!;

    public string Name { get; set; } = default!;

    public string DeviceType { get; set; } = default!;

    public string? Manufacturer { get; set; }

    public string? Model { get; set; }

    public DeviceStateDocument? State { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

internal sealed class DeviceStateDocument
{
    public string Property { get; set; } = default!;

    /// <summary>Serialized name of <c>StateValueType</c> (e.g. "Numeric").</summary>
    public string Type { get; set; } = default!;

    public bool? BoolValue { get; set; }

    public double? NumericValue { get; set; }

    public string? StringValue { get; set; }

    public string? Unit { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
