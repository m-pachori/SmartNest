using FluentAssertions;
using Moq;
using SmartNest.HomeService.Domain;
using SmartNest.HomeService.Domain.ValueObjects;
using SmartNest.HomeService.Dtos;
using SmartNest.HomeService.Handlers;
using SmartNest.HomeService.Repositories;
using SmartNest.Shared.Security;
using Xunit;

namespace SmartNest.HomeService.Tests.Handlers;

public class UpdateHomeHandlerTests
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

    private static UpdateHomeRequest MakeRequest() => new(
        "Renamed Home", "456 Oak Ave", "Chicago", "IL", "60601", "USA",
        "America/New_York", TemperatureUnit.Celsius);

    [Fact]
    public async Task HandleAsync_UpdatesHome_WhenOwnerRoleAndCallerOwnsHome()
    {
        var home = MakeHome(ownerId: "user-1");
        var repository = new Mock<IHomeRepository>();
        repository.Setup(r => r.GetAsync(home.HomeId, It.IsAny<CancellationToken>())).ReturnsAsync(home);
        var handler = new UpdateHomeHandler(repository.Object);
        var user = MakeUser(new[] { "SmartNest.Owner" }, userId: "user-1");

        var result = await handler.HandleAsync(user, home.HomeId, MakeRequest());

        result.Name.Should().Be("Renamed Home");
        result.City.Should().Be("Chicago");
        repository.Verify(r => r.UpdateAsync(home, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Throws_WhenCallerIsNotOwnerRole()
    {
        var repository = new Mock<IHomeRepository>();
        var handler = new UpdateHomeHandler(repository.Object);
        var user = MakeUser(new[] { "SmartNest.Technician" });

        var act = () => handler.HandleAsync(user, "home-1", MakeRequest());

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task HandleAsync_Throws_WhenCallerDoesNotOwnHome()
    {
        var home = MakeHome(ownerId: "owner-1");
        var repository = new Mock<IHomeRepository>();
        repository.Setup(r => r.GetAsync(home.HomeId, It.IsAny<CancellationToken>())).ReturnsAsync(home);
        var handler = new UpdateHomeHandler(repository.Object);
        var user = MakeUser(new[] { "SmartNest.Owner" }, userId: "someone-else");

        var act = () => handler.HandleAsync(user, home.HomeId, MakeRequest());

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task HandleAsync_Throws_WhenHomeNotFound()
    {
        var repository = new Mock<IHomeRepository>();
        repository.Setup(r => r.GetAsync("home-1", It.IsAny<CancellationToken>())).ReturnsAsync((Home?)null);
        var handler = new UpdateHomeHandler(repository.Object);
        var user = MakeUser(new[] { "SmartNest.Owner" });

        var act = () => handler.HandleAsync(user, "home-1", MakeRequest());

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }
}
