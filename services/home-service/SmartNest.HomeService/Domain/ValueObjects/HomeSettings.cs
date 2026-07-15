namespace SmartNest.HomeService.Domain.ValueObjects;

public enum TemperatureUnit
{
    Celsius,
    Fahrenheit,
}

/// <summary>Per-home preferences value object.</summary>
public sealed record HomeSettings(string TimeZone, TemperatureUnit TemperatureUnit)
{
    public string TimeZone { get; init; } = !string.IsNullOrWhiteSpace(TimeZone)
        ? TimeZone
        : throw new ArgumentException("TimeZone is required.", nameof(TimeZone));
}
