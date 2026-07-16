using FluentAssertions;
using Moq;
using SmartNest.PlatformService.Events.Alert;
using SmartNest.PlatformService.Handlers.Alert;
using SmartNest.PlatformService.Infrastructure;
using SmartNest.PlatformService.Repositories.Alert;
using SmartNest.Shared.Events;

namespace SmartNest.PlatformService.Tests.Handlers.Alert;

public class CreateAlertHandlerTests
{
    [Fact]
    public async Task HandleAsync_PersistsAlert_SendsNotification_AndPublishesEvents()
    {
        var repository = new Mock<IAlertRepository>();
        var notificationSender = new Mock<INotificationSender>();
        var eventPublisher = new Mock<IEventPublisher>();
        var handler = new CreateAlertHandler(repository.Object, notificationSender.Object, new AlertEventPublisher(eventPublisher.Object));

        var alert = await handler.HandleAsync(
            "home-1", "device-1", SmartNest.PlatformService.Domain.Alert.ValueObjects.AlertSeverity.Critical, "Smoke detected", "actor-1");

        alert.HomeId.Should().Be("home-1");
        repository.Verify(r => r.CreateAsync(It.IsAny<SmartNest.PlatformService.Domain.Alert.Alert>(), It.IsAny<CancellationToken>()), Times.Once);
        notificationSender.Verify(n => n.SendAsync("home-1", "Smoke detected", It.IsAny<CancellationToken>()), Times.Once);
        eventPublisher.Verify(
            p => p.PublishAsync("device-events", It.Is<EventEnvelope<AlertRaisedPayload>>(e => e.EventType == "AlertRaised"), It.IsAny<CancellationToken>()),
            Times.Once);
        eventPublisher.Verify(
            p => p.PublishAsync("device-events", It.Is<EventEnvelope<AlertDeliveredPayload>>(e => e.EventType == "AlertDelivered"), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
