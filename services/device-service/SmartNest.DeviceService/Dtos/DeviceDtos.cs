using SmartNest.DeviceService.Domain.ValueObjects;

namespace SmartNest.DeviceService.Dtos;

public sealed record RegisterDeviceRequest(string Name, string DeviceType, string? Manufacturer, string? Model);

public sealed record StateValueRequest(StateValueType Type, bool? BoolValue, double? NumericValue, string? StringValue, string? Unit);

public sealed record UpdateDeviceStateRequest(string Property, StateValueRequest Value);

public sealed record StateValueResponse(StateValueType Type, bool? BoolValue, double? NumericValue, string? StringValue, string? Unit)
{
    public static StateValueResponse FromDomain(StateValue value) =>
        new(value.Type, value.BoolValue, value.NumericValue, value.StringValue, value.Unit);
}

public sealed record DeviceResponse(
    string DeviceId,
    string HomeId,
    string Name,
    string DeviceType,
    string? Manufacturer,
    string? Model,
    string? CurrentProperty,
    StateValueResponse? CurrentState,
    DateTimeOffset? StateUpdatedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static DeviceResponse FromDomain(Domain.Device device) => new(
        DeviceId: device.DeviceId,
        HomeId: device.HomeId,
        Name: device.Metadata.Name,
        DeviceType: device.Metadata.DeviceType,
        Manufacturer: device.Metadata.Manufacturer,
        Model: device.Metadata.Model,
        CurrentProperty: device.State?.Property,
        CurrentState: device.State is null ? null : StateValueResponse.FromDomain(device.State.Value),
        StateUpdatedAt: device.State?.UpdatedAt,
        CreatedAt: device.CreatedAt,
        UpdatedAt: device.UpdatedAt);
}

/// <summary>Maps the wire-format <see cref="StateValueRequest"/> to the domain <see cref="StateValue"/>.</summary>
public static class StateValueRequestExtensions
{
    public static StateValue ToDomain(this StateValueRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return request.Type switch
        {
            StateValueType.Boolean => StateValue.FromBoolean(
                request.BoolValue ?? throw new ArgumentException("BoolValue is required for Boolean state values.")),
            StateValueType.Numeric => StateValue.FromNumeric(
                request.NumericValue ?? throw new ArgumentException("NumericValue is required for Numeric state values."),
                request.Unit),
            StateValueType.Text => StateValue.FromText(
                request.StringValue ?? throw new ArgumentException("StringValue is required for Text state values.")),
            _ => throw new ArgumentException($"Unknown StateValueType: {request.Type}"),
        };
    }
}
