using FluentAssertions;
using SmartNest.HomeService.Domain;
using SmartNest.HomeService.Domain.Events;
using SmartNest.HomeService.Domain.ValueObjects;
using Xunit;

namespace SmartNest.HomeService.Tests.Domain;

public class HomeTests
{
    private static Address MakeAddress() => new("123 Main St", "Springfield", "IL", "62701", "USA");

    private static HomeSettings MakeSettings() => new("America/Chicago", TemperatureUnit.Fahrenheit);

    [Fact]
    public void Create_SetsPropertiesAndRaisesHomeCreatedEvent()
    {
        var address = MakeAddress();
        var settings = MakeSettings();

        var home = Home.Create("owner-1", "My Home", address, settings);

        home.HomeId.Should().NotBeNullOrWhiteSpace();
        home.OwnerId.Should().Be("owner-1");
        home.Name.Should().Be("My Home");
        home.Address.Should().Be(address);
        home.Settings.Should().Be(settings);
        home.Rooms.Should().BeEmpty();
        home.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<HomeCreatedDomainEvent>()
            .Which.HomeId.Should().Be(home.HomeId);
    }

    [Theory]
    [InlineData("", "Home")]
    [InlineData(" ", "Home")]
    [InlineData("owner-1", "")]
    [InlineData("owner-1", " ")]
    public void Create_Throws_WhenOwnerIdOrNameMissing(string ownerId, string name)
    {
        var act = () => Home.Create(ownerId, name, MakeAddress(), MakeSettings());

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AddRoom_AddsRoomAndRaisesRoomAddedEvent()
    {
        var home = Home.Create("owner-1", "My Home", MakeAddress(), MakeSettings());
        home.ClearDomainEvents();

        var room = home.AddRoom("Kitchen", "kitchen");

        home.Rooms.Should().ContainSingle().Which.Should().BeSameAs(room);
        room.Name.Should().Be("Kitchen");
        room.RoomType.Should().Be("kitchen");
        home.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<RoomAddedDomainEvent>()
            .Which.Should().BeEquivalentTo(new RoomAddedDomainEvent(home.HomeId, room.RoomId));
    }

    [Fact]
    public void AddRoom_Throws_WhenDuplicateNameCaseInsensitive()
    {
        var home = Home.Create("owner-1", "My Home", MakeAddress(), MakeSettings());
        home.AddRoom("Kitchen");

        var act = () => home.AddRoom("kitchen");

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void RemoveRoom_RemovesExistingRoom()
    {
        var home = Home.Create("owner-1", "My Home", MakeAddress(), MakeSettings());
        var room = home.AddRoom("Kitchen");

        home.RemoveRoom(room.RoomId);

        home.Rooms.Should().BeEmpty();
    }

    [Fact]
    public void RemoveRoom_Throws_WhenRoomDoesNotExist()
    {
        var home = Home.Create("owner-1", "My Home", MakeAddress(), MakeSettings());

        var act = () => home.RemoveRoom("non-existent-room-id");

        act.Should().Throw<KeyNotFoundException>();
    }

    [Fact]
    public void UpdateDetails_UpdatesFieldsAndTimestamp()
    {
        var home = Home.Create("owner-1", "My Home", MakeAddress(), MakeSettings());
        var originalUpdatedAt = home.UpdatedAt;
        var newAddress = new Address("456 Oak Ave", "Chicago", "IL", "60601", "USA");
        var newSettings = new HomeSettings("America/New_York", TemperatureUnit.Celsius);

        home.UpdateDetails("Renamed Home", newAddress, newSettings);

        home.Name.Should().Be("Renamed Home");
        home.Address.Should().Be(newAddress);
        home.Settings.Should().Be(newSettings);
        home.UpdatedAt.Should().BeOnOrAfter(originalUpdatedAt);
    }

    [Fact]
    public void MarkDeleted_RaisesHomeDeletedEvent()
    {
        var home = Home.Create("owner-1", "My Home", MakeAddress(), MakeSettings());
        home.ClearDomainEvents();

        home.MarkDeleted();

        home.DomainEvents.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new HomeDeletedDomainEvent(home.HomeId));
    }

    [Fact]
    public void ClearDomainEvents_EmptiesEventList()
    {
        var home = Home.Create("owner-1", "My Home", MakeAddress(), MakeSettings());

        home.ClearDomainEvents();

        home.DomainEvents.Should().BeEmpty();
    }
}
