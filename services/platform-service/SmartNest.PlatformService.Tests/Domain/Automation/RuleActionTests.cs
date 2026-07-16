using FluentAssertions;
using SmartNest.PlatformService.Domain.Automation.ValueObjects;

namespace SmartNest.PlatformService.Tests.Domain.Automation;

public class RuleActionTests
{
    [Theory]
    [InlineData("Info")]
    [InlineData("warning")]
    [InlineData("CRITICAL")]
    public void RaiseAlert_Accepts_ValidSeverityNames_CaseInsensitive(string severity)
    {
        var action = RuleAction.RaiseAlert(severity, "message");

        action.AlertSeverity.Should().Be(severity);
    }

    [Fact]
    public void RaiseAlert_Throws_WhenSeverityIsNotAValidEnumName()
    {
        var act = () => RuleAction.RaiseAlert("SuperUrgent", "message");

        act.Should().Throw<ArgumentException>()
            .WithMessage("*SuperUrgent*not a valid alert severity*");
    }

    [Fact]
    public void RaiseAlert_Throws_WhenSeverityIsEmpty()
    {
        var act = () => RuleAction.RaiseAlert("", "message");

        act.Should().Throw<ArgumentException>();
    }
}
