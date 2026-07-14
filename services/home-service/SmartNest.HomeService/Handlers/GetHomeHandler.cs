using SmartNest.HomeService.Dtos;
using SmartNest.HomeService.Repositories;
using SmartNest.Shared.Security;

namespace SmartNest.HomeService.Handlers;

/// <summary>Handles <c>GET /homes/{id}</c>. Owner/Technician/Guest may read; caller must own the home.</summary>
public sealed class GetHomeHandler
{
    private readonly IHomeRepository _repository;

    public GetHomeHandler(IHomeRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<HomeResponse> HandleAsync(
        CurrentUser user,
        string homeId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (string.IsNullOrWhiteSpace(homeId))
            throw new ArgumentException("HomeId is required.", nameof(homeId));

        AuthorizationGuard.RequireRole(user, "SmartNest.Owner", "SmartNest.Technician", "SmartNest.Guest");

        var home = await _repository.GetAsync(homeId, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Home '{homeId}' was not found.");

        AuthorizationGuard.RequireOwnership(user, home.OwnerId);

        return HomeResponse.FromDomain(home);
    }
}
