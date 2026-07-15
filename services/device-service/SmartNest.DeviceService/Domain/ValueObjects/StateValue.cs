namespace SmartNest.DeviceService.Domain.ValueObjects;

/// <summary>Discriminates which field of <see cref="StateValue"/> is populated.</summary>
public enum StateValueType
{
    Boolean,
    Numeric,
    Text,
}

/// <summary>
/// Typed device state value - supports the three shapes smartnest-plan.md's Device Service
/// intent calls out ("typed: on/off, temperature, etc."). Exactly one of
/// <see cref="BoolValue"/>/<see cref="NumericValue"/>/<see cref="StringValue"/> is populated,
/// matching <see cref="Type"/>. Kept as an explicit discriminated shape (rather than
/// object/dynamic) so the <c>DeviceStateChanged</c> event payload schema stays stable for
/// the Audit Service consumer.
/// </summary>
public sealed record StateValue
{
    public StateValueType Type { get; private init; }

    public bool? BoolValue { get; private init; }

    public double? NumericValue { get; private init; }

    public string? StringValue { get; private init; }

    public string? Unit { get; private init; }

    private StateValue()
    {
    }

    public static StateValue FromBoolean(bool value) => new()
    {
        Type = StateValueType.Boolean,
        BoolValue = value,
    };

    public static StateValue FromNumeric(double value, string? unit = null) => new()
    {
        Type = StateValueType.Numeric,
        NumericValue = value,
        Unit = unit,
    };

    public static StateValue FromText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Text state value is required.", nameof(value));

        return new StateValue { Type = StateValueType.Text, StringValue = value };
    }

    /// <summary>Reconstructs a <see cref="StateValue"/> from storage without re-validating invariants.</summary>
    internal static StateValue Rehydrate(StateValueType type, bool? boolValue, double? numericValue, string? stringValue, string? unit) =>
        new()
        {
            Type = type,
            BoolValue = boolValue,
            NumericValue = numericValue,
            StringValue = stringValue,
            Unit = unit,
        };

    /// <summary>
    /// Stringified representation used for the <c>DeviceStateChanged</c> event payload's
    /// <c>oldValue</c>/<c>newValue</c> fields, matching the schema in smartnest-plan.md's
    /// "Event Sourcing Strategy" section (all example values are strings).
    /// </summary>
    public string ToPayloadString() => Type switch
    {
        StateValueType.Boolean => BoolValue!.Value.ToString(),
        StateValueType.Numeric => NumericValue!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
        StateValueType.Text => StringValue!,
        _ => throw new InvalidOperationException($"Unknown StateValueType: {Type}"),
    };
}
