using FluentAssertions;
using SmartNest.PlatformService.Domain.Automation;
using SmartNest.PlatformService.Domain.Automation.ValueObjects;

namespace SmartNest.PlatformService.Tests.Domain.Automation;

public class RuleTests
{
    private static Condition MakeCondition() => new("temperature", ConditionOperator.GreaterThan, "30");

    private static RuleAction MakeAlertAction() => RuleAction.RaiseAlert("Warning", "Too hot");

    [Fact]
    public void Create_SetsProperties()
    {
        var rule = Rule.Create("home-1", "device-1", "Hot Alert", MakeCondition(), MakeAlertAction());

        rule.HomeId.Should().Be("home-1");
        rule.DeviceId.Should().Be("device-1");
        rule.Enabled.Should().BeTrue();
        rule.RuleId.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void AppliesTo_ReturnsTrue_WhenDeviceIdMatchesOrRuleIsHomeWide()
    {
        var scoped = Rule.Create("home-1", "device-1", "Rule", MakeCondition(), MakeAlertAction());
        var homeWide = Rule.Create("home-1", null, "Rule", MakeCondition(), MakeAlertAction());

        scoped.AppliesTo("device-1").Should().BeTrue();
        scoped.AppliesTo("device-2").Should().BeFalse();
        homeWide.AppliesTo("device-99").Should().BeTrue();
    }

    [Fact]
    public void AppliesTo_ReturnsFalse_WhenDisabled()
    {
        var rule = Rule.Create("home-1", null, "Rule", MakeCondition(), MakeAlertAction());
        rule.UpdateDetails(rule.Name, rule.Condition, rule.Action, enabled: false);

        rule.AppliesTo("device-1").Should().BeFalse();
    }

    [Fact]
    public void UpdateDetails_UpdatesFieldsAndTimestamp()
    {
        var rule = Rule.Create("home-1", null, "Rule", MakeCondition(), MakeAlertAction());
        var newCondition = new Condition("humidity", ConditionOperator.LessThan, "20");

        rule.UpdateDetails("Renamed", newCondition, MakeAlertAction(), enabled: true);

        rule.Name.Should().Be("Renamed");
        rule.Condition.Should().Be(newCondition);
    }
}
