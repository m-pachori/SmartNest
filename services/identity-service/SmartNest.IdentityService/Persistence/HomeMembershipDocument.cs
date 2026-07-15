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

    public string UserId { get; set; } = default!;

    public string Role { get; set; } = default!;

    public string AssignedByUserId { get; set; } = default!;

    public DateTimeOffset AssignedAt { get; set; }

    /// <summary>Serialized name of <c>MembershipStatus</c> (e.g. "Active").</summary>
    public string Status { get; set; } = default!;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
