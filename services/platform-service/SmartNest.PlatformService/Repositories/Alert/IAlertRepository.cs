namespace SmartNest.PlatformService.Repositories.Alert;

public interface IAlertRepository
{
    Task<Domain.Alert.Alert?> GetAsync(string homeId, string alertId, CancellationToken cancellationToken = default);

    Task CreateAsync(Domain.Alert.Alert alert, CancellationToken cancellationToken = default);

    Task UpdateAsync(Domain.Alert.Alert alert, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Domain.Alert.Alert>> GetByHomeIdAsync(string homeId, CancellationToken cancellationToken = default);
}
