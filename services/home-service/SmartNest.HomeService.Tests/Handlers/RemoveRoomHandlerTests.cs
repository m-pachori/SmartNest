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
    public async Task HandleAsync_RemovesRoom_WhenOwnerRoleAndCallerOwnsHome()
    {
        var home = MakeHome(ownerId: "user-1");
        var room = home.AddRoom("Kitchen");
        var repository = new Mock<IHomeRepository>();
        repository.Setup(r => r.GetAsync(home.HomeId, It.IsAny<CancellationToken>())).ReturnsAsync(home);
        var handler = new RemoveRoomHandler(repository.Object);
        var user = MakeUser(new[] { "SmartNest.Owner" }, userId: "user-1");

        await handler.HandleAsync(user, home.HomeId, room.RoomId);

        home.Rooms.Should().BeEmpty();
        repository.Verify(r => r.UpdateAsync(home, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Throws_WhenCallerIsNotOwnerRole()
    {
        var repository = new Mock<IHomeRepository>();
        var handler = new RemoveRoomHandler(repository.Object);
        var user = MakeUser(new[] { "SmartNest.Guest" });

        var act = () => handler.HandleAsync(user, "home-1", "room-1");

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task HandleAsync_Throws_WhenCallerDoesNotOwnHome()
    {
        var home = MakeHome(ownerId: "owner-1");
        var repository = new Mock<IHomeRepository>();
        repository.Setup(r => r.GetAsync(home.HomeId, It.IsAny<CancellationToken>())).ReturnsAsync(home);
        var handler = new RemoveRoomHandler(repository.Object);
        var user = MakeUser(new[] { "SmartNest.Owner" }, userId: "someone-else");

        var act = () => handler.HandleAsync(user, home.HomeId, "room-1");

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task HandleAsync_Throws_WhenHomeNotFound()
    {
        var repository = new Mock<IHomeRepository>();
        repository.Setup(r => r.GetAsync("home-1", It.IsAny<CancellationToken>())).ReturnsAsync((Home?)null);
        var handler = new RemoveRoomHandler(repository.Object);
        var user = MakeUser(new[] { "SmartNest.Owner" });

        var act = () => handler.HandleAsync(user, "home-1", "room-1");

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task HandleAsync_Throws_WhenRoomNotFound()
    {
        var home = MakeHome(ownerId: "user-1");
        var repository = new Mock<IHomeRepository>();
        repository.Setup(r => r.GetAsync(home.HomeId, It.IsAny<CancellationToken>())).ReturnsAsync(home);
        var handler = new RemoveRoomHandler(repository.Object);
        var user = MakeUser(new[] { "SmartNest.Owner" }, userId: "user-1");

        var act = () => handler.HandleAsync(user, home.HomeId, "non-existent-room");

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }
}
