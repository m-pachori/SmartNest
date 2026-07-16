using Microsoft.Azure.Cosmos;
using SmartNest.PlatformService.Domain.Automation;
using SmartNest.PlatformService.Persistence.Automation;
using SmartNest.Shared.Persistence;

namespace SmartNest.PlatformService.Repositories.Automation;

/// <summary>
/// Cosmos DB-backed <see cref="IRuleRepository"/>. Container: <c>rules</c>, partition key:
/// <c>/homeId</c> (already provisioned in Task 1).
/// </summary>
internal sealed class CosmosRuleRepository : CosmosRepositoryBase<RuleDocument>, IRuleRepository
{
    public CosmosRuleRepository(Container container) : base(container)
    {
    }

    public async Task<Rule?> GetAsync(string homeId, string ruleId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(homeId))
            throw new ArgumentException("HomeId is required.", nameof(homeId));
        if (string.IsNullOrWhiteSpace(ruleId))
            throw new ArgumentException("RuleId is required.", nameof(ruleId));

        var document = await GetAsync(ruleId, partitionKeyValue: homeId, cancellationToken).ConfigureAwait(false);
        return document?.ToDomain();
    }

    public async Task CreateAsync(Rule rule, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rule);

        await CreateAsync(rule.ToDocument(), partitionKeyValue: rule.HomeId, cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAsync(Rule rule, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rule);

        await UpsertAsync(rule.ToDocument(), partitionKeyValue: rule.HomeId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> DeleteAsync(string homeId, string ruleId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(homeId))
            throw new ArgumentException("HomeId is required.", nameof(homeId));
        if (string.IsNullOrWhiteSpace(ruleId))
            throw new ArgumentException("RuleId is required.", nameof(ruleId));

        return await DeleteAsync(ruleId, partitionKeyValue: homeId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Rule>> GetEnabledByHomeIdAsync(string homeId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(homeId))
            throw new ArgumentException("HomeId is required.", nameof(homeId));

        var query = new QueryDefinition("SELECT * FROM c WHERE c.homeId = @homeId AND c.enabled = true")
            .WithParameter("@homeId", homeId);

        var results = new List<Rule>();
        using var iterator = Container.GetItemQueryIterator<RuleDocument>(query, requestOptions: new QueryRequestOptions
        {
            PartitionKey = new PartitionKey(homeId),
        });

        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
            results.AddRange(page.Select(d => d.ToDomain()));
        }

        return results;
    }
}
