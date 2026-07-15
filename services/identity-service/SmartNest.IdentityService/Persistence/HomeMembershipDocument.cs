using Newtonsoft.Json;

namespace SmartNest.IdentityService.Persistence;

/// <summary>
/// Cosmos DB persistence model for the <c>users</c> container (partition key
/// <c>/homeId</c>). Kept separate from the <see cref="SmartNest.IdentityService.Domain.HomeMembership"/>
/// aggregate so the domain model stays persistence-ignorant.
/// </summary>
internal sealed class HomeMembershipDocument
{
    [JsonProperty("id")]
    public string Id { get; set; } = default!;

    [JsonProperty("homeId")]
    public string HomeId { get; set; } = default!;

    [JsonProperty("userId")]
    public string UserId { get; set; } = default!;

    [JsonProperty("role")]
    public string Role { get; set; } = default!;

    [JsonProperty("assignedByUserId")]
    public string AssignedByUserId { get; set; } = default!;

    [JsonProperty("assignedAt")]
    public DateTimeOffset AssignedAt { get; set; }

    /// <summary>Serialized name of <c>MembershipStatus</c> (e.g. "Active").</summary>
    [JsonProperty("status")]
    public string Status { get; set; } = default!;

    [JsonProperty("createdAt")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonProperty("updatedAt")]
    public DateTimeOffset UpdatedAt { get; set; }
}
