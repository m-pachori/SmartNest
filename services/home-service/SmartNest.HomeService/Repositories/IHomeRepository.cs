using SmartNest.HomeService.Domain;

namespace SmartNest.HomeService.Repositories;

public interface IHomeRepository
{
    Task<Home?> GetAsync(string homeId, CancellationToken cancellationToken = default);

    Task CreateAsync(Home home, CancellationToken cancellationToken = default);

    Task UpdateAsync(Home home, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(string homeId, CancellationToken cancellationToken = default);
}
