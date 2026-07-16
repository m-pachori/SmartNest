using FluentAssertions;
using SmartNest.PlatformService.Domain.Alert.ValueObjects;

namespace SmartNest.PlatformService.Tests.Domain.Alert;

public class AlertTests
{
    [Fact]
    public void Raise_SetsProperties_AndNotAcknowledged()
    {
        var alert = SmartNest.PlatformService.Domain.Alert.Alert.Raise("home-1", "device-1", AlertSeverity.Warning, "Too hot");

        alert.HomeId.Should().Be("home-1");
        alert.DeviceId.Should().Be("device-1");
        alert.Severity.Should().Be(AlertSeverity.Warning);
        alert.Acknowledged.Should().BeFalse();
        alert.AcknowledgedAt.Should().BeNull();
    }

    [Fact]
    public void Acknowledge_SetsAcknowledgedAndTimestamp()
    {
        var alert = SmartNest.PlatformService.Domain.Alert.Alert.Raise("home-1", "device-1", AlertSeverity.Critical, "Uh oh");

        alert.Acknowledge();

        alert.Acknowledged.Should().BeTrue();
        alert.AcknowledgedAt.Should().NotBeNull();
    }

    [Fact]
    public void Acknowledge_IsIdempotent()
    {
        var alert = SmartNest.PlatformService.Domain.Alert.Alert.Raise("home-1", "device-1", AlertSeverity.Info, "FYI");
        alert.Acknowledge();
        var firstAcknowledgedAt = alert.AcknowledgedAt;

        alert.Acknowledge();

        alert.AcknowledgedAt.Should().Be(firstAcknowledgedAt);
    }

    [Fact]
    public void Raise_Throws_WhenMessageIsMissing()
    {
        var act = () => SmartNest.PlatformService.Domain.Alert.Alert.Raise("home-1", "device-1", AlertSeverity.Info, "");

        act.Should().Throw<ArgumentException>();
    }
}
