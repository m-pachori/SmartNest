using FluentAssertions;
using SmartNest.DeviceService.Domain;
using SmartNest.DeviceService.Domain.Events;
using SmartNest.DeviceService.Domain.ValueObjects;
using Xunit;

namespace SmartNest.DeviceService.Tests.Domain;

public class DeviceTests
{
    private static DeviceMetadata MakeMetadata() => new("Living Room Thermostat", "thermostat", "Acme", "T-100");

    [Fact]
    public void Register_SetsPropertiesAndRaisesDeviceRegisteredEvent()
    {
        var metadata = MakeMetadata();

        var device = Device.Register("home-1", metadata);

        device.DeviceId.Should().NotBeNullOrWhiteSpace();
        device.HomeId.Should().Be("home-1");
        device.Metadata.Should().Be(metadata);
        device.State.Should().BeNull();
        device.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<DeviceRegisteredDomainEvent>()
            .Which.Should().BeEquivalentTo(new DeviceRegisteredDomainEvent(device.DeviceId, device.HomeId));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Register_Throws_WhenHomeIdMissing(string homeId)
    {
        var act = () => Device.Register(homeId, MakeMetadata());

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdateState_SetsStateAndRaisesDeviceStateChangedEvent_WithNullOldValue_WhenFirstUpdate()
    {
        var device = Device.Register("home-1", MakeMetadata());
        device.ClearDomainEvents();

        device.UpdateState("temperature", StateValue.FromNumeric(21.5, "celsius"));

        device.State.Should().NotBeNull();
        device.State!.Property.Should().Be("temperature");
        device.State.Value.NumericValue.Should().Be(21.5);
        device.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<DeviceStateChangedDomainEvent>()
            .Which.Should().BeEquivalentTo(new DeviceStateChangedDomainEvent(
                device.DeviceId, device.HomeId, "temperature", null, "21.5", "celsius"));
    }

    [Fact]
    public void UpdateState_CarriesForwardOldValue_OnSubsequentUpdate()
    {
        var device = Device.Register("home-1", MakeMetadata());
        device.UpdateState("temperature", StateValue.FromNumeric(21.5, "celsius"));
        device.ClearDomainEvents();

        device.UpdateState("temperature", StateValue.FromNumeric(25, "celsius"));

        device.DomainEvents.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new DeviceStateChangedDomainEvent(
                device.DeviceId, device.HomeId, "temperature", "21.5", "25", "celsius"));
    }

    [Fact]
    public void UpdateState_Throws_WhenPropertyMissing()
    {
        var device = Device.Register("home-1", MakeMetadata());

        var act = () => device.UpdateState("", StateValue.FromBoolean(true));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void MarkRemoved_RaisesDeviceRemovedEvent()
    {
        var device = Device.Register("home-1", MakeMetadata());
        device.ClearDomainEvents();

        device.MarkRemoved();

        device.DomainEvents.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new DeviceRemovedDomainEvent(device.DeviceId, device.HomeId));
    }
}

public class StateValueTests
{
    [Fact]
    public void FromBoolean_RoundTripsToPayloadString()
    {
        var value = StateValue.FromBoolean(true);

        value.Type.Should().Be(StateValueType.Boolean);
        value.ToPayloadString().Should().Be("True");
    }

    [Fact]
    public void FromNumeric_RoundTripsToPayloadString()
    {
        var value = StateValue.FromNumeric(72.3, "fahrenheit");

        value.Type.Should().Be(StateValueType.Numeric);
        value.Unit.Should().Be("fahrenheit");
        value.ToPayloadString().Should().Be("72.3");
    }

    [Fact]
    public void FromText_Throws_WhenValueMissing()
    {
        var act = () => StateValue.FromText(" ");

        act.Should().Throw<ArgumentException>();
    }
}
