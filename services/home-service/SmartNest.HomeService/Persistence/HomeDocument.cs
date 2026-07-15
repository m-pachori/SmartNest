using Newtonsoft.Json;

namespace SmartNest.HomeService.Persistence;

/// <summary>
/// Cosmos DB persistence model for the <c>homes</c> container (partition key
/// <c>/homeId</c>). Kept separate from the <see cref="SmartNest.HomeService.Domain.Home"/>
/// aggregate so the domain model stays persistence-ignorant.
/// </summary>
internal sealed class HomeDocument
{
    [JsonProperty("id")]
    public string Id { get; set; } = default!;

    [JsonProperty("homeId")]
    public string HomeId { get; set; } = default!;

    public string OwnerId { get; set; } = default!;

    public string Name { get; set; } = default!;

    public AddressDocument Address { get; set; } = default!;

    public HomeSettingsDocument Settings { get; set; } = default!;

    public List<RoomDocument> Rooms { get; set; } = new();

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

internal sealed class AddressDocument
{
    public string Street { get; set; } = default!;

    public string City { get; set; } = default!;

    public string State { get; set; } = default!;

    public string PostalCode { get; set; } = default!;

    public string Country { get; set; } = default!;
}

internal sealed class HomeSettingsDocument
{
    public string TimeZone { get; set; } = default!;

    /// <summary>Serialized name of <c>TemperatureUnit</c> (e.g. "Celsius").</summary>
    public string TemperatureUnit { get; set; } = default!;
}

internal sealed class RoomDocument
{
    public string RoomId { get; set; } = default!;

    public string Name { get; set; } = default!;

    public string? RoomType { get; set; }
}
