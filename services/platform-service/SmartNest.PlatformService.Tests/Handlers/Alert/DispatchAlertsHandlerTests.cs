using System.Text.Json;
using FluentAssertions;
using Moq;
using SmartNest.PlatformService.Events.Alert;
using SmartNest.PlatformService.Events.Shared;
using SmartNest.PlatformService.Handlers.Alert;
using SmartNest.PlatformService.Infrastructure;
using SmartNest.PlatformService.Repositories.Alert;
using SmartNest.Shared.Events;

namespace SmartNest.PlatformService.Tests.Handlers.Alert;

public class DispatchAlertsHandlerTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static string MakeMessage(string property, string newValue, string? unit = null)
    {
        var envelope = EventEnvelope<DeviceStateChangedPayload>.Create(
            eventType: "DeviceStateChanged",
            aggregateId: "device-1",
            aggregateType: "Device",
            actorId: "user-1",
            homeId: "home-1",
            payload: new DeviceStateChangedPayload("device-1", "home-1", property, null, newValue, unit));

        return JsonSerializer.Serialize(envelope, JsonOptions);
    }

    [Fact]
    public async Task HandleAsync_DoesNothing_WhenPayloadIsNull()
    {
        var message = """
        {
            "eventId": "evt-1",
            "eventType": "DeviceStateChanged",
            "aggregateId": "device-1",
            "aggregateType": "Device",
            "occurredAt": "2026-07-16T00:00:00Z",
            "actorId": "user-1",
            "homeId": "home-1",
            "correlationId": "corr-1",
            "payload": null
        }
        """;

        var alertRepository = new Mock<IAlertRepository>();
        var notificationSender = new Mock<INotificationSender>();
        var eventPublisher = new Mock<IEventPublisher>();
        var createAlertHandler = new CreateAlertHandler(alertRepository.Object, notificationSender.Object, new AlertEventPublisher(eventPublisher.Object));
        var handler = new DispatchAlertsHandler(createAlertHandler);

        var act = () => handler.HandleAsync(message);

        await act.Should().NotThrowAsync();
        alertRepository.Verify(r => r.CreateAsync(It.IsAny<SmartNest.PlatformService.Domain.Alert.Alert>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_RaisesAlert_WhenTemperatureExceedsThreshold()
    {
        var alertRepository = new Mock<IAlertRepository>();
        var notificationSender = new Mock<INotificationSender>();
        var eventPublisher = new Mock<IEventPublisher>();
        var createAlertHandler = new CreateAlertHandler(alertRepository.Object, notificationSender.Object, new AlertEventPublisher(eventPublisher.Object));
        var handler = new DispatchAlertsHandler(createAlertHandler);

        await handler.HandleAsync(MakeMessage("temperature", "35", "celsius"));

        alertRepository.Verify(r => r.CreateAsync(It.IsAny<SmartNest.PlatformService.Domain.Alert.Alert>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_DoesNothing_WhenTemperatureBelowThreshold()
    {
        var alertRepository = new Mock<IAlertRepository>();
        var notificationSender = new Mock<INotificationSender>();
        var eventPublisher = new Mock<IEventPublisher>();
        var createAlertHandler = new CreateAlertHandler(alertRepository.Object, notificationSender.Object, new AlertEventPublisher(eventPublisher.Object));
        var handler = new DispatchAlertsHandler(createAlertHandler);

        await handler.HandleAsync(MakeMessage("temperature", "20", "celsius"));

        alertRepository.Verify(r => r.CreateAsync(It.IsAny<SmartNest.PlatformService.Domain.Alert.Alert>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_DoesNothing_ForUnrelatedProperty()
    {
        var alertRepository = new Mock<IAlertRepository>();
        var notificationSender = new Mock<INotificationSender>();
        var eventPublisher = new Mock<IEventPublisher>();
        var createAlertHandler = new CreateAlertHandler(alertRepository.Object, notificationSender.Object, new AlertEventPublisher(eventPublisher.Object));
        var handler = new DispatchAlertsHandler(createAlertHandler);

        await handler.HandleAsync(MakeMessage("power", "on"));

        alertRepository.Verify(r => r.CreateAsync(It.IsAny<SmartNest.PlatformService.Domain.Alert.Alert>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
