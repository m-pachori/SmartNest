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
    private static CurrentUser MakeUser(string userId, params string[] roles) => new()
    {
        UserId = userId,
        Roles = roles,
    };

    private static Device MakeDevice(string homeId = "home-1") =>
        Device.Register(homeId, new DeviceMetadata("Thermostat", "thermostat", null, null));

    [Fact]
    public async Task HandleAsync_ReturnsDevice_WhenCallerOwnsTheHome_RegardlessOfRole()
    {
        var device = MakeDevice();
        var repository = new Mock<IDeviceRepository>();
        repository.Setup(r => r.GetAsync(device.DeviceId, It.IsAny<CancellationToken>())).ReturnsAsync(device);
        var homeOwnership = new Mock<IHomeOwnershipRepository>();
        homeOwnership.Setup(h => h.GetOwnerIdAsync("home-1", It.IsAny<CancellationToken>())).ReturnsAsync("user-1");
        var handler = new GetDeviceHandler(repository.Object, homeOwnership.Object);
        var user = MakeUser("user-1", "SmartNest.Guest");

        var result = await handler.HandleAsync(user, device.DeviceId);

        result.DeviceId.Should().Be(device.DeviceId);
    }

    [Fact]
    public async Task HandleAsync_Throws_WhenDeviceNotFound()
    {
        var repository = new Mock<IDeviceRepository>();
        repository.Setup(r => r.GetAsync("missing", It.IsAny<CancellationToken>())).ReturnsAsync((Device?)null);
        var homeOwnership = new Mock<IHomeOwnershipRepository>();
        var handler = new GetDeviceHandler(repository.Object, homeOwnership.Object);
        var user = MakeUser("user-1", "SmartNest.Owner");

        var act = () => handler.HandleAsync(user, "missing");

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task HandleAsync_Throws_WhenHomeDoesNotExist()
    {
        var device = MakeDevice(homeId: "home-1");
        var repository = new Mock<IDeviceRepository>();
        repository.Setup(r => r.GetAsync(device.DeviceId, It.IsAny<CancellationToken>())).ReturnsAsync(device);
        var homeOwnership = new Mock<IHomeOwnershipRepository>();
        homeOwnership.Setup(h => h.GetOwnerIdAsync("home-1", It.IsAny<CancellationToken>())).ReturnsAsync((string?)null);
        var handler = new GetDeviceHandler(repository.Object, homeOwnership.Object);
        var user = MakeUser("user-1", "SmartNest.Owner");

        var act = () => handler.HandleAsync(user, device.DeviceId);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task HandleAsync_Throws_WhenCallerDoesNotOwnTheHome()
    {
        var device = MakeDevice(homeId: "home-1");
        var repository = new Mock<IDeviceRepository>();
        repository.Setup(r => r.GetAsync(device.DeviceId, It.IsAny<CancellationToken>())).ReturnsAsync(device);
        var homeOwnership = new Mock<IHomeOwnershipRepository>();
        homeOwnership.Setup(h => h.GetOwnerIdAsync("home-1", It.IsAny<CancellationToken>())).ReturnsAsync("someone-else");
        var handler = new GetDeviceHandler(repository.Object, homeOwnership.Object);
        var user = MakeUser("user-1", "SmartNest.Owner");

        var act = () => handler.HandleAsync(user, device.DeviceId);

        await act.Should().ThrowAsync<ForbiddenException>();
    }
}
