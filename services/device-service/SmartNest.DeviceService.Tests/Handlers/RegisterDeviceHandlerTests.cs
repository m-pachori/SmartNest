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
    private static CurrentUser MakeUser(string? homeId, params string[] roles) => new()
    {
        UserId = "user-1",
        Roles = roles,
        HomeId = homeId,
    };

    private static RegisterDeviceRequest MakeRequest() => new(
        Name: "Living Room Thermostat",
        DeviceType: "thermostat",
        Manufacturer: "Acme",
        Model: "T-100");

    [Fact]
    public async Task HandleAsync_RegistersDeviceAndPublishesEvent_WhenCallerIsOwnerWithMatchingHomeId()
    {
        var repository = new Mock<IDeviceRepository>();
        var eventPublisher = new Mock<IEventPublisher>();
        var handler = new RegisterDeviceHandler(repository.Object, new DeviceEventPublisher(eventPublisher.Object));
        var user = MakeUser("home-1", "SmartNest.Owner");

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
        var eventPublisher = new Mock<IEventPublisher>();
        var handler = new RegisterDeviceHandler(repository.Object, new DeviceEventPublisher(eventPublisher.Object));
        var user = MakeUser("home-1", "SmartNest.Guest");

        var act = () => handler.HandleAsync(user, "home-1", MakeRequest());

        await act.Should().ThrowAsync<ForbiddenException>();
        repository.Verify(r => r.CreateAsync(It.IsAny<Device>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_Throws_WhenHomeIdClaimDoesNotMatchTargetHome()
    {
        var repository = new Mock<IDeviceRepository>();
        var eventPublisher = new Mock<IEventPublisher>();
        var handler = new RegisterDeviceHandler(repository.Object, new DeviceEventPublisher(eventPublisher.Object));
        var user = MakeUser("home-2", "SmartNest.Owner");

        var act = () => handler.HandleAsync(user, "home-1", MakeRequest());

        await act.Should().ThrowAsync<ForbiddenException>();
        repository.Verify(r => r.CreateAsync(It.IsAny<Device>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
