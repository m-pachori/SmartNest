using Microsoft.Azure.Cosmos;
using SmartNest.IdentityService.Domain;
using SmartNest.IdentityService.Persistence;
using SmartNest.Shared.Persistence;

namespace SmartNest.IdentityService.Repositories;

/// <summary>
/// Cosmos DB-backed <see cref="IIdentityRepository"/>. Container: <c>users</c>,
/// partition key: <c>/homeId</c> (see smartnest-plan.md Task 4).
/// </summary>
internal sealed class CosmosIdentityRepository : CosmosRepositoryBase<HomeMembershipDocument>, IIdentityRepository
{
    public CosmosIdentityRepository(Container container) : base(container)
    {
    }

    public async Task<HomeMembership?> GetAsync(string membershipId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(membershipId))
            throw new ArgumentException("MembershipId is required.", nameof(membershipId));

        // Cross-partition query: the flat /users/{id}/role route doesn't carry homeId.
        var query = new QueryDefinition("SELECT * FROM c WHERE c.id = @id").WithParameter("@id", membershipId);
        using var iterator = Container.GetItemQueryIterator<HomeMembershipDocument>(query);

        if (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);

            // MembershipId is a generated GUID, so more than one match should never happen -
            // guard against it explicitly rather than silently returning an arbitrary match.
            if (response.Count > 1)
            {
                throw new InvalidOperationException(
                    $"Data integrity error: found {response.Count} memberships with id '{membershipId}' across different homes.");
            }

            var document = response.FirstOrDefault();
            if (document is not null)
                return document.ToDomain();
        }

        return null;
    }

    public async Task<HomeMembership?> GetByHomeAndUserAsync(string homeId, string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(homeId))
            throw new ArgumentException("HomeId is required.", nameof(homeId));
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("UserId is required.", nameof(userId));

        // Partition-scoped query (not cross-partition) - homeId is already known here.
        // Only matches Active memberships: a re-invite after deactivation should create a
        // fresh membership record rather than colliding with the old (retained) one.
        var query = new QueryDefinition("SELECT * FROM c WHERE c.UserId = @userId AND c.Status = 'Active'")
            .WithParameter("@userId", userId);
        var requestOptions = new QueryRequestOptions { PartitionKey = new PartitionKey(homeId) };
        using var iterator = Container.GetItemQueryIterator<HomeMembershipDocument>(query, requestOptions: requestOptions);

        if (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
            var document = response.FirstOrDefault();
            if (document is not null)
                return document.ToDomain();
        }

        return null;
    }

    public async Task CreateAsync(HomeMembership membership, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(membership);

        await CreateAsync(membership.ToDocument(), partitionKeyValue: membership.HomeId, cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAsync(HomeMembership membership, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(membership);

        await UpsertAsync(membership.ToDocument(), partitionKeyValue: membership.HomeId, cancellationToken).ConfigureAwait(false);
    }
}
