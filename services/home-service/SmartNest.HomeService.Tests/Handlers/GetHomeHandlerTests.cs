using FluentAssertions;
using Moq;
using SmartNest.HomeService.Domain;
using SmartNest.HomeService.Domain.ValueObjects;
using SmartNest.HomeService.Handlers;
using SmartNest.HomeService.Repositories;
using SmartNest.Shared.Security;
using Xunit;

namespace SmartNest.HomeService.Tests.Handlers;

public class GetHomeHandlerTests
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
    public async Task HandleAsync_ReturnsHome_WhenRoleAllowedAndCallerOwnsHome()
    {
        var home = MakeHome(ownerId: "user-1");
        var repository = new Mock<IHomeRepository>();
        repository.Setup(r => r.GetAsync(home.HomeId, It.IsAny<CancellationToken>())).ReturnsAsync(home);
        var handler = new GetHomeHandler(repository.Object);
        var user = MakeUser(new[] { "SmartNest.Guest" }, userId: "user-1");

        var result = await handler.HandleAsync(user, home.HomeId);

        result.HomeId.Should().Be(home.HomeId);
    }

    [Fact]
    public async Task HandleAsync_Throws_WhenCallerDoesNotOwnHome()
    {
        var home = MakeHome(ownerId: "owner-1");
        var repository = new Mock<IHomeRepository>();
        repository.Setup(r => r.GetAsync(home.HomeId, It.IsAny<CancellationToken>())).ReturnsAsync(home);
        var handler = new GetHomeHandler(repository.Object);
        var user = MakeUser(new[] { "SmartNest.Owner" }, userId: "someone-else");

        var act = () => handler.HandleAsync(user, home.HomeId);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task HandleAsync_Throws_WhenCallerHasNoRecognizedRole()
    {
        var repository = new Mock<IHomeRepository>();
        var handler = new GetHomeHandler(repository.Object);
        var user = MakeUser(Array.Empty<string>());

        var act = () => handler.HandleAsync(user, "home-1");

        await act.Should().ThrowAsync<ForbiddenException>();
        repository.Verify(r => r.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_Throws_WhenHomeNotFound()
    {
        var repository = new Mock<IHomeRepository>();
        repository.Setup(r => r.GetAsync("home-1", It.IsAny<CancellationToken>())).ReturnsAsync((Home?)null);
        var handler = new GetHomeHandler(repository.Object);
        var user = MakeUser(new[] { "SmartNest.Owner" });

        var act = () => handler.HandleAsync(user, "home-1");

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }
}
