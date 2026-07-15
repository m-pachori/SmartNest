namespace SmartNest.DeviceService.Domain.ValueObjects;

/// <summary>Device identification/metadata value object.</summary>
public sealed record DeviceMetadata(string Name, string DeviceType, string? Manufacturer, string? Model)
{
    public string Name { get; init; } = Require(Name, nameof(Name));

    public string DeviceType { get; init; } = Require(DeviceType, nameof(DeviceType));

    private static string Require(string value, string paramName) =>
        !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException($"{paramName} is required.", paramName);
}
