namespace SmartNest.PlatformService.Domain.Automation.ValueObjects;

/// <summary>Comparison operator used to evaluate a <see cref="Condition"/> against an incoming value.</summary>
public enum ConditionOperator
{
    GreaterThan,
    LessThan,
    Equals,
}

/// <summary>
/// A simple field/operator/value condition evaluated against a <c>DeviceStateChanged</c>
/// event's <c>property</c>/<c>newValue</c> (e.g. "temperature &gt; 30"). Deliberately a
/// plain comparison (no expression-language dependency) - matches
/// smartnest-plan.md Task 5's example.
/// </summary>
public sealed record Condition(string Field, ConditionOperator Operator, string Value)
{
    public string Field { get; init; } = Require(Field, nameof(Field));

    public string Value { get; init; } = Require(Value, nameof(Value));

    private static string Require(string value, string paramName) =>
        !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException($"{paramName} is required.", paramName);

    /// <summary>
    /// Evaluates this condition against an incoming device event: <paramref name="property"/>
    /// must match <see cref="Field"/>, and <paramref name="actualValue"/> is compared to
    /// <see cref="Value"/> using <see cref="Operator"/>. Numeric comparison is attempted
    /// first (for GreaterThan/LessThan); falls back to ordinal string comparison.
    /// </summary>
    public bool Matches(string property, string actualValue)
    {
        if (!string.Equals(property, Field, StringComparison.OrdinalIgnoreCase))
            return false;

        if (Operator == ConditionOperator.Equals)
            return string.Equals(actualValue, Value, StringComparison.OrdinalIgnoreCase);

        if (!double.TryParse(actualValue, System.Globalization.CultureInfo.InvariantCulture, out var actualNumeric) ||
            !double.TryParse(Value, System.Globalization.CultureInfo.InvariantCulture, out var thresholdNumeric))
        {
            return false;
        }

        return Operator == ConditionOperator.GreaterThan
            ? actualNumeric > thresholdNumeric
            : actualNumeric < thresholdNumeric;
    }
}
