using FluentAssertions;
using Moq;
using SmartNest.DeviceService.Domain;
using SmartNest.DeviceService.Dtos;
using SmartNest.DeviceService.Events;
using SmartNest.DeviceService.Handlers;
using SmartNest.DeviceService.Repositories;
using SmartNest.Shared.Events;
using SmartNest.Shared.Security;
using Xunit;

namespace SmartNest.DeviceService.Tests.Handlers;

public class RegisterDeviceHandlerTests
{
    private static CurrentUser MakeUser(string userId, params string[] roles) => new()
    {
        UserId = userId,
        Roles = roles,
    };

    private static RegisterDeviceRequest MakeRequest() => new(
        Name: "Living Room Thermostat",
        DeviceType: "thermostat",
        Manufacturer: "Acme",
        Model: "T-100");

    [Fact]
    public async Task HandleAsync_RegistersDeviceAndPublishesEvent_WhenCallerOwnsTheHome()
    {
        var repository = new Mock<IDeviceRepository>();
        var homeOwnership = new Mock<IHomeOwnershipRepository>();
        homeOwnership.Setup(h => h.GetOwnerIdAsync("home-1", It.IsAny<CancellationToken>())).ReturnsAsync("user-1");
        var eventPublisher = new Mock<IEventPublisher>();
        var handler = new RegisterDeviceHandler(repository.Object, homeOwnership.Object, new DeviceEventPublisher(eventPublisher.Object));
        var user = MakeUser("user-1", "SmartNest.Owner");

        var result = await handler.HandleAsync(user, "home-1", MakeRequest());

        result.HomeId.Should().Be("home-1");
        result.Name.Should().Be("Living Room Thermostat");
        repository.Verify(r => r.CreateAsync(It.IsAny<Device>(), It.IsAny<CancellationToken>()), Times.Once);
        eventPublisher.Verify(
            p => p.PublishAsync(
                "device-events",
                It.Is<EventEnvelope<DeviceRegisteredPayload>>(e => e.EventType == "DeviceRegistered"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Throws_WhenCallerIsGuest()
    {
        var repository = new Mock<IDeviceRepository>();
        var homeOwnership = new Mock<IHomeOwnershipRepository>();
        var eventPublisher = new Mock<IEventPublisher>();
        var handler = new RegisterDeviceHandler(repository.Object, homeOwnership.Object, new DeviceEventPublisher(eventPublisher.Object));
        var user = MakeUser("user-1", "SmartNest.Guest");

        var act = () => handler.HandleAsync(user, "home-1", MakeRequest());

        await act.Should().ThrowAsync<ForbiddenException>();
        repository.Verify(r => r.CreateAsync(It.IsAny<Device>(), It.IsAny<CancellationToken>()), Times.Never);
        homeOwnership.Verify(h => h.GetOwnerIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_Throws_WhenCallerDoesNotOwnTheHome()
    {
        var repository = new Mock<IDeviceRepository>();
        var homeOwnership = new Mock<IHomeOwnershipRepository>();
        homeOwnership.Setup(h => h.GetOwnerIdAsync("home-1", It.IsAny<CancellationToken>())).ReturnsAsync("someone-else");
        var eventPublisher = new Mock<IEventPublisher>();
        var handler = new RegisterDeviceHandler(repository.Object, homeOwnership.Object, new DeviceEventPublisher(eventPublisher.Object));
        var user = MakeUser("user-1", "SmartNest.Owner");

        var act = () => handler.HandleAsync(user, "home-1", MakeRequest());

        await act.Should().ThrowAsync<ForbiddenException>();
        repository.Verify(r => r.CreateAsync(It.IsAny<Device>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_Throws_WhenHomeDoesNotExist()
    {
        var repository = new Mock<IDeviceRepository>();
        var homeOwnership = new Mock<IHomeOwnershipRepository>();
        homeOwnership.Setup(h => h.GetOwnerIdAsync("home-1", It.IsAny<CancellationToken>())).ReturnsAsync((string?)null);
        var eventPublisher = new Mock<IEventPublisher>();
        var handler = new RegisterDeviceHandler(repository.Object, homeOwnership.Object, new DeviceEventPublisher(eventPublisher.Object));
        var user = MakeUser("user-1", "SmartNest.Owner");

        var act = () => handler.HandleAsync(user, "home-1", MakeRequest());

        await act.Should().ThrowAsync<KeyNotFoundException>();
        repository.Verify(r => r.CreateAsync(It.IsAny<Device>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
