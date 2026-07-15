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

public class AddRoomHandlerTests
{
    private static CurrentUser MakeUser(string[] roles, string userId = "user-1") => new()
    {
        UserId = userId,
        Roles = roles,
    };

    private static Home MakeHome(string ownerId = "user-1") => Home.Create(
        ownerId, "My Home",
        new Address("123 Main St", "Springfield", "IL", "62701", "USA"),
        new HomeSettings("America/Chicago", TemperatureUnit.Fahrenheit));

    [Fact]
    public async Task HandleAsync_AddsRoomAndPublishesRoomAddedEvent()
    {
        var home = MakeHome(ownerId: "user-1");
        var repository = new Mock<IHomeRepository>();
        repository.Setup(r => r.GetAsync(home.HomeId, It.IsAny<CancellationToken>())).ReturnsAsync(home);
        var eventPublisher = new Mock<IEventPublisher>();
        var handler = new AddRoomHandler(repository.Object, new HomeEventPublisher(eventPublisher.Object));
        var user = MakeUser(new[] { "SmartNest.Owner" }, userId: "user-1");

        var result = await handler.HandleAsync(user, home.HomeId, new AddRoomRequest("Kitchen", "kitchen"));

        result.Name.Should().Be("Kitchen");
        repository.Verify(r => r.UpdateAsync(home, It.IsAny<CancellationToken>()), Times.Once);
        eventPublisher.Verify(
            p => p.PublishAsync(
                "home-events",
                It.Is<EventEnvelope<RoomAddedPayload>>(e => e.EventType == "RoomAdded"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Throws_WhenCallerIsNotOwnerRole()
    {
        var repository = new Mock<IHomeRepository>();
        var eventPublisher = new Mock<IEventPublisher>();
        var handler = new AddRoomHandler(repository.Object, new HomeEventPublisher(eventPublisher.Object));
        var user = MakeUser(new[] { "SmartNest.Technician" });

        var act = () => handler.HandleAsync(user, "home-1", new AddRoomRequest("Kitchen", null));

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task HandleAsync_Throws_WhenCallerDoesNotOwnHome()
    {
        var home = MakeHome(ownerId: "owner-1");
        var repository = new Mock<IHomeRepository>();
        repository.Setup(r => r.GetAsync(home.HomeId, It.IsAny<CancellationToken>())).ReturnsAsync(home);
        var eventPublisher = new Mock<IEventPublisher>();
        var handler = new AddRoomHandler(repository.Object, new HomeEventPublisher(eventPublisher.Object));
        var user = MakeUser(new[] { "SmartNest.Owner" }, userId: "someone-else");

        var act = () => handler.HandleAsync(user, home.HomeId, new AddRoomRequest("Kitchen", null));

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task HandleAsync_Throws_WhenHomeNotFound()
    {
        var repository = new Mock<IHomeRepository>();
        repository.Setup(r => r.GetAsync("home-1", It.IsAny<CancellationToken>())).ReturnsAsync((Home?)null);
        var eventPublisher = new Mock<IEventPublisher>();
        var handler = new AddRoomHandler(repository.Object, new HomeEventPublisher(eventPublisher.Object));
        var user = MakeUser(new[] { "SmartNest.Owner" });

        var act = () => handler.HandleAsync(user, "home-1", new AddRoomRequest("Kitchen", null));

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task HandleAsync_Throws_WhenDuplicateRoomName()
    {
        var home = MakeHome(ownerId: "user-1");
        home.AddRoom("Kitchen");
        var repository = new Mock<IHomeRepository>();
        repository.Setup(r => r.GetAsync(home.HomeId, It.IsAny<CancellationToken>())).ReturnsAsync(home);
        var eventPublisher = new Mock<IEventPublisher>();
        var handler = new AddRoomHandler(repository.Object, new HomeEventPublisher(eventPublisher.Object));
        var user = MakeUser(new[] { "SmartNest.Owner" }, userId: "user-1");

        var act = () => handler.HandleAsync(user, home.HomeId, new AddRoomRequest("kitchen", null));

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
