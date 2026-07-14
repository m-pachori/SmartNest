using FluentAssertions;
using Moq;
using SmartNest.HomeService.Domain;
using SmartNest.HomeService.Domain.ValueObjects;
using SmartNest.HomeService.Events;
using SmartNest.HomeService.Handlers;
using SmartNest.HomeService.Repositories;
using SmartNest.Shared.Events;
using SmartNest.Shared.Security;
using Xunit;

namespace SmartNest.HomeService.Tests.Handlers;

public class DeleteHomeHandlerTests
{
    private static CurrentUser MakeUser(string[] roles, string? homeId) => new()
    {
        UserId = "user-1",
        Roles = roles,
        HomeId = homeId,
    };

    private static Home MakeHome() => Home.Create(
        "owner-1", "My Home",
        new Address("123 Main St", "Springfield", "IL", "62701", "USA"),
        new HomeSettings("America/Chicago", TemperatureUnit.Fahrenheit));

    [Fact]
    public async Task HandleAsync_DeletesHomeAndPublishesHomeDeletedEvent()
    {
        var home = MakeHome();
        var repository = new Mock<IHomeRepository>();
        repository.Setup(r => r.GetAsync(home.HomeId, It.IsAny<CancellationToken>())).ReturnsAsync(home);
        repository.Setup(r => r.DeleteAsync(home.HomeId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var eventPublisher = new Mock<IEventPublisher>();
        var handler = new DeleteHomeHandler(repository.Object, new HomeEventPublisher(eventPublisher.Object));
        var user = MakeUser(new[] { "SmartNest.Owner" }, home.HomeId);

        await handler.HandleAsync(user, home.HomeId);

        repository.Verify(r => r.DeleteAsync(home.HomeId, It.IsAny<CancellationToken>()), Times.Once);
        eventPublisher.Verify(
            p => p.PublishAsync(
                "home-events",
                It.Is<EventEnvelope<HomeDeletedPayload>>(e => e.EventType == "HomeDeleted"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Throws_WhenCallerIsNotOwner()
    {
        var repository = new Mock<IHomeRepository>();
        var eventPublisher = new Mock<IEventPublisher>();
        var handler = new DeleteHomeHandler(repository.Object, new HomeEventPublisher(eventPublisher.Object));
        var user = MakeUser(new[] { "SmartNest.Guest" }, "home-1");

        var act = () => handler.HandleAsync(user, "home-1");

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task HandleAsync_Throws_WhenHomeNotFound()
    {
        var repository = new Mock<IHomeRepository>();
        repository.Setup(r => r.GetAsync("home-1", It.IsAny<CancellationToken>())).ReturnsAsync((Home?)null);
        var eventPublisher = new Mock<IEventPublisher>();
        var handler = new DeleteHomeHandler(repository.Object, new HomeEventPublisher(eventPublisher.Object));
        var user = MakeUser(new[] { "SmartNest.Owner" }, "home-1");

        var act = () => handler.HandleAsync(user, "home-1");

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }
}
