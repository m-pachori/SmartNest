using SmartNest.PlatformService.Persistence.Summary;

namespace SmartNest.PlatformService.Repositories.Summary;

public interface ISummaryRepository
{
    Task UpsertAsync(DailySummaryDocument summary, CancellationToken cancellationToken = default);

    Task<DailySummaryDocument?> GetAsync(string homeId, string date, CancellationToken cancellationToken = default);
}
