using FluentAssertions;
using SmartNest.PlatformService.Domain.Automation.ValueObjects;

namespace SmartNest.PlatformService.Tests.Domain.Automation;

public class ConditionTests
{
    [Theory]
    [InlineData(35, 30, true)]
    [InlineData(25, 30, false)]
    public void Matches_GreaterThan_ComparesNumerically(double actual, double threshold, bool expected)
    {
        var condition = new Condition("temperature", ConditionOperator.GreaterThan, threshold.ToString());

        condition.Matches("temperature", actual.ToString()).Should().Be(expected);
    }

    [Fact]
    public void Matches_ReturnsFalse_WhenPropertyDoesNotMatchField()
    {
        var condition = new Condition("temperature", ConditionOperator.GreaterThan, "30");

        condition.Matches("humidity", "35").Should().BeFalse();
    }

    [Fact]
    public void Matches_Equals_ComparesOrdinalIgnoreCase()
    {
        var condition = new Condition("status", ConditionOperator.Equals, "On");

        condition.Matches("status", "on").Should().BeTrue();
    }

    [Fact]
    public void Constructor_Throws_WhenFieldIsMissing()
    {
        var act = () => new Condition("", ConditionOperator.Equals, "value");

        act.Should().Throw<ArgumentException>();
    }
}
