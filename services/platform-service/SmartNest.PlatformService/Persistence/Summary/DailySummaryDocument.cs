using Newtonsoft.Json;

namespace SmartNest.PlatformService.Persistence.Summary;

/// <summary>
/// Cosmos DB persistence model for the <c>summaries</c> container (partition key
/// <c>/homeId</c>, already provisioned in Task 1). Document id and partition key follow
/// <c>{homeId}_{yyyy-MM-dd}</c> so re-runs upsert idempotently (Task 8).
/// </summary>
public sealed class DailySummaryDocument
{
    [JsonProperty("id")]
    public string Id { get; set; } = default!;

    [JsonProperty("homeId")]
    public string HomeId { get; set; } = default!;

    /// <summary>The summarized calendar day, formatted <c>yyyy-MM-dd</c> (UTC).</summary>
    public string Date { get; set; } = default!;

    /// <summary>Event counts by <c>eventType</c> (e.g. "DeviceStateChanged": 42).</summary>
    public Dictionary<string, int> EventCounts { get; set; } = new();

    public int TotalEvents { get; set; }

    public DateTimeOffset GeneratedAt { get; set; }
}
