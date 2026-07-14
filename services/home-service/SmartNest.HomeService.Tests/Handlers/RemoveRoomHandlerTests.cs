using FluentAssertions;
using Moq;
using SmartNest.HomeService.Domain;
using SmartNest.HomeService.Domain.ValueObjects;
using SmartNest.HomeService.Handlers;
using SmartNest.HomeService.Repositories;
using SmartNest.Shared.Security;
using Xunit;

namespace SmartNest.HomeService.Tests.Handlers;

public class RemoveRoomHandlerTests
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
    public async Task HandleAsync_RemovesRoom_WhenOwnerAndHomeIdMatch()
    {
        var home = MakeHome();
        var room = home.AddRoom("Kitchen");
        var repository = new Mock<IHomeRepository>();
        repository.Setup(r => r.GetAsync(home.HomeId, It.IsAny<CancellationToken>())).ReturnsAsync(home);
        var handler = new RemoveRoomHandler(repository.Object);
        var user = MakeUser(new[] { "SmartNest.Owner" }, home.HomeId);

        await handler.HandleAsync(user, home.HomeId, room.RoomId);

        home.Rooms.Should().BeEmpty();
        repository.Verify(r => r.UpdateAsync(home, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Throws_WhenCallerIsNotOwner()
    {
        var repository = new Mock<IHomeRepository>();
        var handler = new RemoveRoomHandler(repository.Object);
        var user = MakeUser(new[] { "SmartNest.Guest" }, "home-1");

        var act = () => handler.HandleAsync(user, "home-1", "room-1");

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task HandleAsync_Throws_WhenHomeNotFound()
    {
        var repository = new Mock<IHomeRepository>();
        repository.Setup(r => r.GetAsync("home-1", It.IsAny<CancellationToken>())).ReturnsAsync((Home?)null);
        var handler = new RemoveRoomHandler(repository.Object);
        var user = MakeUser(new[] { "SmartNest.Owner" }, "home-1");

        var act = () => handler.HandleAsync(user, "home-1", "room-1");

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task HandleAsync_Throws_WhenRoomNotFound()
    {
        var home = MakeHome();
        var repository = new Mock<IHomeRepository>();
        repository.Setup(r => r.GetAsync(home.HomeId, It.IsAny<CancellationToken>())).ReturnsAsync(home);
        var handler = new RemoveRoomHandler(repository.Object);
        var user = MakeUser(new[] { "SmartNest.Owner" }, home.HomeId);

        var act = () => handler.HandleAsync(user, home.HomeId, "non-existent-room");

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }
}
