using SmartNest.HomeService.Domain.ValueObjects;

namespace SmartNest.HomeService.Dtos;

public sealed record CreateHomeRequest(
    string Name,
    string Street,
    string City,
    string State,
    string PostalCode,
    string Country,
    string TimeZone,
    TemperatureUnit TemperatureUnit);

public sealed record UpdateHomeRequest(
    string Name,
    string Street,
    string City,
    string State,
    string PostalCode,
    string Country,
    string TimeZone,
    TemperatureUnit TemperatureUnit);

public sealed record AddRoomRequest(string Name, string? RoomType);

public sealed record RoomResponse(string RoomId, string Name, string? RoomType);

public sealed record HomeResponse(
    string HomeId,
    string OwnerId,
    string Name,
    string Street,
    string City,
    string State,
    string PostalCode,
    string Country,
    string TimeZone,
    TemperatureUnit TemperatureUnit,
    IReadOnlyList<RoomResponse> Rooms,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static HomeResponse FromDomain(Domain.Home home) => new(
        HomeId: home.HomeId,
        OwnerId: home.OwnerId,
        Name: home.Name,
        Street: home.Address.Street,
        City: home.Address.City,
        State: home.Address.State,
        PostalCode: home.Address.PostalCode,
        Country: home.Address.Country,
        TimeZone: home.Settings.TimeZone,
        TemperatureUnit: home.Settings.TemperatureUnit,
        Rooms: home.Rooms.Select(r => new RoomResponse(r.RoomId, r.Name, r.RoomType)).ToList(),
        CreatedAt: home.CreatedAt,
        UpdatedAt: home.UpdatedAt);
}
