using SmartNest.HomeService.Domain.Events;
using SmartNest.HomeService.Domain.ValueObjects;

namespace SmartNest.HomeService.Domain;

/// <summary>
/// Home aggregate root. Enforces invariants for the Home bounded context (see
/// smartnest-plan.md "Bounded Contexts & Aggregates"). Only publishes the three
/// documented domain events: HomeCreated, RoomAdded, HomeDeleted.
/// </summary>
public sealed class Home
{
    private readonly List<Room> _rooms = new();
    private readonly List<IDomainEvent> _domainEvents = new();

    public string HomeId { get; private set; } = default!;

    public string OwnerId { get; private set; } = default!;

    public string Name { get; private set; } = default!;

    public Address Address { get; private set; } = default!;

    public HomeSettings Settings { get; private set; } = default!;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyList<Room> Rooms => _rooms.AsReadOnly();

    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    // For repository/document mapping only.
    private Home()
    {
    }

    public static Home Create(string ownerId, string name, Address address, HomeSettings settings)
    {
        if (string.IsNullOrWhiteSpace(ownerId))
            throw new ArgumentException("OwnerId is required.", nameof(ownerId));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Home name is required.", nameof(name));
        ArgumentNullException.ThrowIfNull(address);
        ArgumentNullException.ThrowIfNull(settings);

        var now = DateTimeOffset.UtcNow;
        var home = new Home
        {
            HomeId = Guid.NewGuid().ToString(),
            OwnerId = ownerId,
            Name = name,
            Address = address,
            Settings = settings,
            CreatedAt = now,
            UpdatedAt = now,
        };

        home._domainEvents.Add(new HomeCreatedDomainEvent(home.HomeId));
        return home;
    }

    /// <summary>Reconstructs an existing Home from storage without raising domain events.</summary>
    internal static Home Rehydrate(
        string homeId,
        string ownerId,
        string name,
        Address address,
        HomeSettings settings,
        IEnumerable<Room> rooms,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        var home = new Home
        {
            HomeId = homeId,
            OwnerId = ownerId,
            Name = name,
            Address = address,
            Settings = settings,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
        };
        home._rooms.AddRange(rooms);
        return home;
    }

    public void UpdateDetails(string name, Address address, HomeSettings settings)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Home name is required.", nameof(name));
        ArgumentNullException.ThrowIfNull(address);
        ArgumentNullException.ThrowIfNull(settings);

        Name = name;
        Address = address;
        Settings = settings;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public Room AddRoom(string name, string? roomType = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Room name is required.", nameof(name));
        if (_rooms.Any(r => string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"A room named '{name}' already exists in this home.");

        var room = new Room(Guid.NewGuid().ToString(), name, roomType);
        _rooms.Add(room);
        UpdatedAt = DateTimeOffset.UtcNow;
        _domainEvents.Add(new RoomAddedDomainEvent(HomeId, room.RoomId));
        return room;
    }

    public void RemoveRoom(string roomId)
    {
        if (string.IsNullOrWhiteSpace(roomId))
            throw new ArgumentException("RoomId is required.", nameof(roomId));

        var room = _rooms.FirstOrDefault(r => r.RoomId == roomId)
            ?? throw new KeyNotFoundException($"Room '{roomId}' was not found in this home.");

        _rooms.Remove(room);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Raises the HomeDeleted domain event. Call before the repository deletes the document.</summary>
    public void MarkDeleted()
    {
        _domainEvents.Add(new HomeDeletedDomainEvent(HomeId));
    }

    public void ClearDomainEvents() => _domainEvents.Clear();
}
