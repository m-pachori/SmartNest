using FluentAssertions;
using Moq;
using SmartNest.DeviceService.Domain;
using SmartNest.DeviceService.Domain.ValueObjects;
using SmartNest.DeviceService.Events;
using SmartNest.DeviceService.Handlers;
using SmartNest.DeviceService.Repositories;
using SmartNest.Shared.Events;
using SmartNest.Shared.Security;
using Xunit;

namespace SmartNest.DeviceService.Tests.Handlers;

public class RemoveDeviceHandlerTests
{
    private static CurrentUser MakeUser(string userId, params string[] roles) => new()
    {
        UserId = userId,
        Roles = roles,
    };

    private static Device MakeDevice(string homeId = "home-1") =>
        Device.Register(homeId, new DeviceMetadata("Thermostat", "thermostat", null, null));

    [Fact]
    public async Task HandleAsync_RemovesDeviceAndPublishesEvent_WhenCallerIsOwnerAndOwnsTheHome()
    {
        var device = MakeDevice();
        var repository = new Mock<IDeviceRepository>();
        repository.Setup(r => r.GetAsync(device.DeviceId, It.IsAny<CancellationToken>())).ReturnsAsync(device);
        repository.Setup(r => r.DeleteAsync(device.DeviceId, device.HomeId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var homeOwnership = new Mock<IHomeOwnershipRepository>();
        homeOwnership.Setup(h => h.GetOwnerIdAsync("home-1", It.IsAny<CancellationToken>())).ReturnsAsync("user-1");
        var eventPublisher = new Mock<IEventPublisher>();
        var handler = new RemoveDeviceHandler(repository.Object, homeOwnership.Object, new DeviceEventPublisher(eventPublisher.Object));
        var user = MakeUser("user-1", "SmartNest.Owner");

        await handler.HandleAsync(user, device.DeviceId);

        repository.Verify(r => r.DeleteAsync(device.DeviceId, device.HomeId, It.IsAny<CancellationToken>()), Times.Once);
        eventPublisher.Verify(
            p => p.PublishAsync(
                "device-events",
                It.Is<EventEnvelope<DeviceRemovedPayload>>(e => e.EventType == "DeviceRemoved"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Throws_WhenCallerIsGuest()
    {
        var repository = new Mock<IDeviceRepository>();
        var homeOwnership = new Mock<IHomeOwnershipRepository>();
        var eventPublisher = new Mock<IEventPublisher>();
        var handler = new RemoveDeviceHandler(repository.Object, homeOwnership.Object, new DeviceEventPublisher(eventPublisher.Object));
        var user = MakeUser("user-1", "SmartNest.Guest");

        var act = () => handler.HandleAsync(user, "device-1");

        await act.Should().ThrowAsync<ForbiddenException>();
        repository.Verify(r => r.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_Throws_WhenDeviceNotFound()
    {
        var repository = new Mock<IDeviceRepository>();
        repository.Setup(r => r.GetAsync("missing", It.IsAny<CancellationToken>())).ReturnsAsync((Device?)null);
        var homeOwnership = new Mock<IHomeOwnershipRepository>();
        var eventPublisher = new Mock<IEventPublisher>();
        var handler = new RemoveDeviceHandler(repository.Object, homeOwnership.Object, new DeviceEventPublisher(eventPublisher.Object));
        var user = MakeUser("user-1", "SmartNest.Owner");

        var act = () => handler.HandleAsync(user, "missing");

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
        var eventPublisher = new Mock<IEventPublisher>();
        var handler = new RemoveDeviceHandler(repository.Object, homeOwnership.Object, new DeviceEventPublisher(eventPublisher.Object));
        var user = MakeUser("user-1", "SmartNest.Owner");

        var act = () => handler.HandleAsync(user, device.DeviceId);

        await act.Should().ThrowAsync<ForbiddenException>();
        repository.Verify(r => r.DeleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
