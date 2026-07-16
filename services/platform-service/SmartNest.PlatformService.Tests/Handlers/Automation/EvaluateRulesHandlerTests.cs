using System.Text.Json;
using FluentAssertions;
using Moq;
using SmartNest.PlatformService.Domain.Automation;
using SmartNest.PlatformService.Domain.Automation.ValueObjects;
using SmartNest.PlatformService.Events.Alert;
using SmartNest.PlatformService.Events.Automation;
using SmartNest.PlatformService.Events.Shared;
using SmartNest.PlatformService.Handlers.Alert;
using SmartNest.PlatformService.Handlers.Automation;
using SmartNest.PlatformService.Infrastructure;
using SmartNest.PlatformService.Repositories.Alert;
using SmartNest.PlatformService.Repositories.Automation;
using SmartNest.Shared.Events;

namespace SmartNest.PlatformService.Tests.Handlers.Automation;

public class EvaluateRulesHandlerTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static string MakeDeviceStateChangedMessage(string homeId, string deviceId, string property, string newValue)
    {
        var envelope = EventEnvelope<DeviceStateChangedPayload>.Create(
            eventType: "DeviceStateChanged",
            aggregateId: deviceId,
            aggregateType: "Device",
            actorId: "user-1",
            homeId: homeId,
            payload: new DeviceStateChangedPayload(deviceId, homeId, property, null, newValue, null));

        return JsonSerializer.Serialize(envelope, JsonOptions);
    }

    [Fact]
    public async Task HandleAsync_CallsDeviceStateClient_WhenChangeDeviceStateRuleMatches()
    {
        var rule = Rule.Create("home-1", "device-1", "Cool Down", new Condition("temperature", ConditionOperator.GreaterThan, "30"),
            RuleAction.ChangeDeviceState("device-2", "power", "off"));

        var ruleRepository = new Mock<IRuleRepository>();
        ruleRepository.Setup(r => r.GetEnabledByHomeIdAsync("home-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SmartNest.PlatformService.Domain.Automation.Rule> { rule });

        var deviceStateClient = new Mock<IDeviceStateClient>();
        var eventPublisher = new Mock<IEventPublisher>();
        var alertRepository = new Mock<IAlertRepository>();
        var notificationSender = new Mock<INotificationSender>();
        var createAlertHandler = new CreateAlertHandler(alertRepository.Object, notificationSender.Object, new AlertEventPublisher(eventPublisher.Object));

        var handler = new EvaluateRulesHandler(
            ruleRepository.Object,
            deviceStateClient.Object,
            createAlertHandler,
            new AutomationEventPublisher(eventPublisher.Object));

        await handler.HandleAsync(MakeDeviceStateChangedMessage("home-1", "device-1", "temperature", "35"));

        deviceStateClient.Verify(c => c.UpdateStateAsync("device-2", "power", "off", It.IsAny<CancellationToken>()), Times.Once);
        eventPublisher.Verify(
            p => p.PublishAsync("device-events", It.Is<EventEnvelope<AutomationExecutedPayload>>(e => e.EventType == "AutomationExecuted"), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_RaisesAlertInProcess_WhenRaiseAlertRuleMatches()
    {
        var rule = Rule.Create("home-1", null, "Hot Alert", new Condition("temperature", ConditionOperator.GreaterThan, "30"),
            RuleAction.RaiseAlert("Warning", "Too hot"));

        var ruleRepository = new Mock<IRuleRepository>();
        ruleRepository.Setup(r => r.GetEnabledByHomeIdAsync("home-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SmartNest.PlatformService.Domain.Automation.Rule> { rule });

        var deviceStateClient = new Mock<IDeviceStateClient>();
        var eventPublisher = new Mock<IEventPublisher>();
        var alertRepository = new Mock<IAlertRepository>();
        var notificationSender = new Mock<INotificationSender>();
        var createAlertHandler = new CreateAlertHandler(alertRepository.Object, notificationSender.Object, new AlertEventPublisher(eventPublisher.Object));

        var handler = new EvaluateRulesHandler(
            ruleRepository.Object,
            deviceStateClient.Object,
            createAlertHandler,
            new AutomationEventPublisher(eventPublisher.Object));

        await handler.HandleAsync(MakeDeviceStateChangedMessage("home-1", "device-1", "temperature", "40"));

        alertRepository.Verify(r => r.CreateAsync(It.IsAny<SmartNest.PlatformService.Domain.Alert.Alert>(), It.IsAny<CancellationToken>()), Times.Once);
        deviceStateClient.Verify(c => c.UpdateStateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_DoesNothing_WhenNoRuleConditionMatches()
    {
        var rule = Rule.Create("home-1", null, "Hot Alert", new Condition("temperature", ConditionOperator.GreaterThan, "30"),
            RuleAction.RaiseAlert("Warning", "Too hot"));

        var ruleRepository = new Mock<IRuleRepository>();
        ruleRepository.Setup(r => r.GetEnabledByHomeIdAsync("home-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SmartNest.PlatformService.Domain.Automation.Rule> { rule });

        var deviceStateClient = new Mock<IDeviceStateClient>();
        var eventPublisher = new Mock<IEventPublisher>();
        var alertRepository = new Mock<IAlertRepository>();
        var notificationSender = new Mock<INotificationSender>();
        var createAlertHandler = new CreateAlertHandler(alertRepository.Object, notificationSender.Object, new AlertEventPublisher(eventPublisher.Object));

        var handler = new EvaluateRulesHandler(
            ruleRepository.Object,
            deviceStateClient.Object,
            createAlertHandler,
            new AutomationEventPublisher(eventPublisher.Object));

        await handler.HandleAsync(MakeDeviceStateChangedMessage("home-1", "device-1", "temperature", "10"));

        alertRepository.Verify(r => r.CreateAsync(It.IsAny<SmartNest.PlatformService.Domain.Alert.Alert>(), It.IsAny<CancellationToken>()), Times.Never);
        deviceStateClient.Verify(c => c.UpdateStateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
