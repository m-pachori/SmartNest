using FluentAssertions;
using Moq;
using SmartNest.DeviceService.Domain;
using SmartNest.DeviceService.Domain.ValueObjects;
using SmartNest.DeviceService.Handlers;
using SmartNest.DeviceService.Repositories;
using SmartNest.Shared.Security;
using Xunit;

namespace SmartNest.DeviceService.Tests.Handlers;

public class GetDeviceHandlerTests
{
    private static CurrentUser MakeUser(string? homeId, params string[] roles) => new()
    {
        UserId = "user-1",
        Roles = roles,
        HomeId = homeId,
    };

    private static Device MakeDevice(string homeId = "home-1") =>
        Device.Register(homeId, new DeviceMetadata("Thermostat", "thermostat", null, null));

    [Fact]
    public async Task HandleAsync_ReturnsDevice_WhenHomeIdMatches_RegardlessOfRole()
    {
        var device = MakeDevice();
        var repository = new Mock<IDeviceRepository>();
        repository.Setup(r => r.GetAsync(device.DeviceId, It.IsAny<CancellationToken>())).ReturnsAsync(device);
        var handler = new GetDeviceHandler(repository.Object);
        var user = MakeUser("home-1", "SmartNest.Guest");

        var result = await handler.HandleAsync(user, device.DeviceId);

        result.DeviceId.Should().Be(device.DeviceId);
    }

    [Fact]
    public async Task HandleAsync_Throws_WhenDeviceNotFound()
    {
        var repository = new Mock<IDeviceRepository>();
        repository.Setup(r => r.GetAsync("missing", It.IsAny<CancellationToken>())).ReturnsAsync((Device?)null);
        var handler = new GetDeviceHandler(repository.Object);
        var user = MakeUser("home-1", "SmartNest.Owner");

        var act = () => handler.HandleAsync(user, "missing");

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task HandleAsync_Throws_WhenHomeIdClaimDoesNotMatch()
    {
        var device = MakeDevice(homeId: "home-1");
        var repository = new Mock<IDeviceRepository>();
        repository.Setup(r => r.GetAsync(device.DeviceId, It.IsAny<CancellationToken>())).ReturnsAsync(device);
        var handler = new GetDeviceHandler(repository.Object);
        var user = MakeUser("home-2", "SmartNest.Owner");

        var act = () => handler.HandleAsync(user, device.DeviceId);

        await act.Should().ThrowAsync<ForbiddenException>();
    }
}
