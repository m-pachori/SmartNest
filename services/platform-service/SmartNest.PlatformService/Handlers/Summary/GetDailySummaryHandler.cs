using SmartNest.PlatformService.Dtos.Summary;
using SmartNest.PlatformService.Repositories.Shared;
using SmartNest.PlatformService.Repositories.Summary;
using SmartNest.Shared.Security;

namespace SmartNest.PlatformService.Handlers.Summary;

/// <summary>Handles <c>GET /summaries/{homeId}?date={date}</c>. Caller must own the home.</summary>
public sealed class GetDailySummaryHandler
{
    private readonly ISummaryRepository _repository;
    private readonly IHomeOwnershipRepository _homeOwnershipRepository;

    public GetDailySummaryHandler(ISummaryRepository repository, IHomeOwnershipRepository homeOwnershipRepository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _homeOwnershipRepository = homeOwnershipRepository ?? throw new ArgumentNullException(nameof(homeOwnershipRepository));
    }

    public async Task<DailySummaryResponse> HandleAsync(CurrentUser user, string homeId, string date, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (string.IsNullOrWhiteSpace(homeId))
            throw new ArgumentException("HomeId is required.", nameof(homeId));
        if (string.IsNullOrWhiteSpace(date))
            throw new ArgumentException("Date is required.", nameof(date));

        var ownerId = await _homeOwnershipRepository.GetOwnerIdAsync(homeId, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Home '{homeId}' was not found.");
        AuthorizationGuard.RequireOwnership(user, ownerId);

        var summary = await _repository.GetAsync(homeId, date, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"No summary found for home '{homeId}' on '{date}'.");

        return DailySummaryResponse.FromDocument(summary);
    }
}
