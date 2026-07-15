using SmartNest.DeviceService.Domain.ValueObjects;

namespace SmartNest.DeviceService.Domain;

/// <summary>
/// The device's single tracked property/value entity (e.g. a thermostat's "temperature",
/// a switch's "power"). Owned by the <see cref="Device"/> aggregate.
/// </summary>
public sealed class DeviceState
{
    public string Property { get; private set; } = default!;

    public StateValue Value { get; private set; } = default!;

    public DateTimeOffset UpdatedAt { get; private set; }

    // For repository/document mapping only.
    private DeviceState()
    {
    }

    internal DeviceState(string property, StateValue value, DateTimeOffset updatedAt)
    {
        if (string.IsNullOrWhiteSpace(property))
            throw new ArgumentException("Property is required.", nameof(property));
        ArgumentNullException.ThrowIfNull(value);

        Property = property;
        Value = value;
        UpdatedAt = updatedAt;
    }

    /// <summary>Used by the repository layer to reconstruct existing state from storage.</summary>
    internal static DeviceState Rehydrate(string property, StateValue value, DateTimeOffset updatedAt) =>
        new(property, value, updatedAt);
}
