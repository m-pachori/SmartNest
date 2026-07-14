using SmartNest.HomeService.Domain.ValueObjects;
using SmartNest.HomeService.Dtos;
using SmartNest.HomeService.Repositories;
using SmartNest.Shared.Security;

namespace SmartNest.HomeService.Handlers;

/// <summary>Handles <c>PUT /homes/{id}</c>. Requires Owner role + caller must own the home.</summary>
public sealed class UpdateHomeHandler
{
    private readonly IHomeRepository _repository;

    public UpdateHomeHandler(IHomeRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<HomeResponse> HandleAsync(
        CurrentUser user,
        string homeId,
        UpdateHomeRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(homeId))
            throw new ArgumentException("HomeId is required.", nameof(homeId));

        AuthorizationGuard.RequireRole(user, "SmartNest.Owner");

        var home = await _repository.GetAsync(homeId, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Home '{homeId}' was not found.");

        AuthorizationGuard.RequireOwnership(user, home.OwnerId);

        var address = new Address(request.Street, request.City, request.State, request.PostalCode, request.Country);
        var settings = new HomeSettings(request.TimeZone, request.TemperatureUnit);
        home.UpdateDetails(request.Name, address, settings);

        await _repository.UpdateAsync(home, cancellationToken).ConfigureAwait(false);

        return HomeResponse.FromDomain(home);
    }
}
