using FluentAssertions;
using Moq;
using SmartNest.HomeService.Domain;
using SmartNest.HomeService.Domain.ValueObjects;
using SmartNest.HomeService.Dtos;
using SmartNest.HomeService.Events;
using SmartNest.HomeService.Handlers;
using SmartNest.HomeService.Repositories;
using SmartNest.Shared.Events;
using SmartNest.Shared.Security;
using Xunit;

namespace SmartNest.HomeService.Tests.Handlers;

public class CreateHomeHandlerTests
{
    private static CurrentUser MakeUser(params string[] roles) => new()
    {
        UserId = "user-1",
        Roles = roles,
    };

    private static CreateHomeRequest MakeRequest() => new(
        Name: "My Home",
        Street: "123 Main St",
        City: "Springfield",
        State: "IL",
        PostalCode: "62701",
        Country: "USA",
        TimeZone: "America/Chicago",
        TemperatureUnit: TemperatureUnit.Fahrenheit);

    [Fact]
    public async Task HandleAsync_CreatesHomeAndPublishesHomeCreatedEvent_WhenCallerIsOwner()
    {
        var repository = new Mock<IHomeRepository>();
        var eventPublisher = new Mock<IEventPublisher>();
        var handler = new CreateHomeHandler(repository.Object, new HomeEventPublisher(eventPublisher.Object));
        var user = MakeUser("SmartNest.Owner");

        var result = await handler.HandleAsync(user, MakeRequest());

        result.OwnerId.Should().Be("user-1");
        result.Name.Should().Be("My Home");
        repository.Verify(r => r.CreateAsync(It.IsAny<Home>(), It.IsAny<CancellationToken>()), Times.Once);
        eventPublisher.Verify(
            p => p.PublishAsync(
                "home-events",
                It.Is<EventEnvelope<HomeCreatedPayload>>(e => e.EventType == "HomeCreated"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Throws_WhenCallerIsNotOwner()
    {
        var repository = new Mock<IHomeRepository>();
        var eventPublisher = new Mock<IEventPublisher>();
        var handler = new CreateHomeHandler(repository.Object, new HomeEventPublisher(eventPublisher.Object));
        var user = MakeUser("SmartNest.Guest");

        var act = () => handler.HandleAsync(user, MakeRequest());

        await act.Should().ThrowAsync<ForbiddenException>();
        repository.Verify(r => r.CreateAsync(It.IsAny<Home>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
