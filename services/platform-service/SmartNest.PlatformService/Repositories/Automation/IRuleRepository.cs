using SmartNest.PlatformService.Domain.Automation;

namespace SmartNest.PlatformService.Repositories.Automation;

public interface IRuleRepository
{
    Task<Rule?> GetAsync(string homeId, string ruleId, CancellationToken cancellationToken = default);

    Task CreateAsync(Rule rule, CancellationToken cancellationToken = default);

    Task UpdateAsync(Rule rule, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(string homeId, string ruleId, CancellationToken cancellationToken = default);

    /// <summary>Returns all enabled rules for a home, used by EvaluateRulesHandler.</summary>
    Task<IReadOnlyList<Rule>> GetEnabledByHomeIdAsync(string homeId, CancellationToken cancellationToken = default);
}
