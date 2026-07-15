using SmartNest.DeviceService.Domain;
using SmartNest.DeviceService.Domain.ValueObjects;

namespace SmartNest.DeviceService.Persistence;

internal static class DeviceDocumentMapper
{
    public static DeviceDocument ToDocument(this Device device)
    {
        ArgumentNullException.ThrowIfNull(device);

        return new DeviceDocument
        {
            Id = device.DeviceId,
            HomeId = device.HomeId,
            Name = device.Metadata.Name,
            DeviceType = device.Metadata.DeviceType,
            Manufacturer = device.Metadata.Manufacturer,
            Model = device.Metadata.Model,
            State = device.State is null
                ? null
                : new DeviceStateDocument
                {
                    Property = device.State.Property,
                    Type = device.State.Value.Type.ToString(),
                    BoolValue = device.State.Value.BoolValue,
                    NumericValue = device.State.Value.NumericValue,
                    StringValue = device.State.Value.StringValue,
                    Unit = device.State.Value.Unit,
                    UpdatedAt = device.State.UpdatedAt,
                },
            CreatedAt = device.CreatedAt,
            UpdatedAt = device.UpdatedAt,
        };
    }

    public static Device ToDomain(this DeviceDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var metadata = new DeviceMetadata(document.Name, document.DeviceType, document.Manufacturer, document.Model);

        DeviceState? state = null;
        if (document.State is not null)
        {
            var value = StateValue.Rehydrate(
                Enum.Parse<StateValueType>(document.State.Type),
                document.State.BoolValue,
                document.State.NumericValue,
                document.State.StringValue,
                document.State.Unit);
            state = DeviceState.Rehydrate(document.State.Property, value, document.State.UpdatedAt);
        }

        return Device.Rehydrate(document.Id, document.HomeId, metadata, state, document.CreatedAt, document.UpdatedAt);
    }
}
