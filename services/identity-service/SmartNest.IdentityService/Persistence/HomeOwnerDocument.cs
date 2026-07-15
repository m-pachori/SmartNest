using Newtonsoft.Json;

namespace SmartNest.IdentityService.Persistence;

/// <summary>
/// Minimal read-only projection of a Home Service <c>homes</c> container document - only
/// the fields needed for the ownership check (see <see cref="Repositories.IHomeOwnershipRepository"/>).
/// Field names/casing must match <c>SmartNest.HomeService.Persistence.HomeDocument</c>
/// exactly, since this reads the same underlying documents from a different service.
/// </summary>
internal sealed class HomeOwnerDocument
{
    [JsonProperty("id")]
    public string Id { get; set; } = default!;

    public string OwnerId { get; set; } = default!;
}
