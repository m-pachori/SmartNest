using SmartNest.HomeService.Domain;
using SmartNest.HomeService.Domain.ValueObjects;
using SmartNest.HomeService.Dtos;
using SmartNest.HomeService.Events;
using SmartNest.HomeService.Repositories;
using SmartNest.Shared.Security;

namespace SmartNest.HomeService.Handlers;

/// <summary>Handles <c>POST /homes</c>. Requires the Owner role (no homeId claim check — see ADR notes).</summary>
public sealed class CreateHomeHandler
{
    private readonly IHomeRepository _repository;
    private readonly HomeEventPublisher _eventPublisher;

    public CreateHomeHandler(IHomeRepository repository, HomeEventPublisher eventPublisher)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _eventPublisher = eventPublisher ?? throw new ArgumentNullException(nameof(eventPublisher));
    }

    public async Task<HomeResponse> HandleAsync(
        CurrentUser user,
        CreateHomeRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(request);

        AuthorizationGuard.RequireRole(user, "SmartNest.Owner");

        var address = new Address(request.Street, request.City, request.State, request.PostalCode, request.Country);
        var settings = new HomeSettings(request.TimeZone, request.TemperatureUnit);
        var home = Home.Create(user.UserId, request.Name, address, settings);

        await _repository.CreateAsync(home, cancellationToken).ConfigureAwait(false);
        await _eventPublisher.PublishAllAsync(home.DomainEvents, user.UserId, cancellationToken).ConfigureAwait(false);
        home.ClearDomainEvents();

        return HomeResponse.FromDomain(home);
    }
}
