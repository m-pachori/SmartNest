using SmartNest.HomeService.Domain;
using SmartNest.HomeService.Domain.ValueObjects;

namespace SmartNest.HomeService.Persistence;

internal static class HomeDocumentMapper
{
    public static HomeDocument ToDocument(this Home home)
    {
        ArgumentNullException.ThrowIfNull(home);

        return new HomeDocument
        {
            Id = home.HomeId,
            HomeId = home.HomeId,
            OwnerId = home.OwnerId,
            Name = home.Name,
            Address = new AddressDocument
            {
                Street = home.Address.Street,
                City = home.Address.City,
                State = home.Address.State,
                PostalCode = home.Address.PostalCode,
                Country = home.Address.Country,
            },
            Settings = new HomeSettingsDocument
            {
                TimeZone = home.Settings.TimeZone,
                TemperatureUnit = home.Settings.TemperatureUnit.ToString(),
            },
            Rooms = home.Rooms
                .Select(r => new RoomDocument { RoomId = r.RoomId, Name = r.Name, RoomType = r.RoomType })
                .ToList(),
            CreatedAt = home.CreatedAt,
            UpdatedAt = home.UpdatedAt,
        };
    }

    public static Home ToDomain(this HomeDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var address = new Address(
            document.Address.Street,
            document.Address.City,
            document.Address.State,
            document.Address.PostalCode,
            document.Address.Country);

        var settings = new HomeSettings(
            document.Settings.TimeZone,
            Enum.Parse<TemperatureUnit>(document.Settings.TemperatureUnit));

        var rooms = document.Rooms.Select(r => Room.Rehydrate(r.RoomId, r.Name, r.RoomType));

        return Home.Rehydrate(
            document.HomeId,
            document.OwnerId,
            document.Name,
            address,
            settings,
            rooms,
            document.CreatedAt,
            document.UpdatedAt);
    }
}
