namespace SmartNest.HomeService.Domain;

/// <summary>Room entity owned by the <see cref="Home"/> aggregate.</summary>
public sealed class Room
{
    public string RoomId { get; private set; } = default!;

    public string Name { get; private set; } = default!;

    public string? RoomType { get; private set; }

    // For repository/document mapping only.
    private Room()
    {
    }

    internal Room(string roomId, string name, string? roomType)
    {
        if (string.IsNullOrWhiteSpace(roomId))
            throw new ArgumentException("RoomId is required.", nameof(roomId));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Room name is required.", nameof(name));

        RoomId = roomId;
        Name = name;
        RoomType = roomType;
    }

    /// <summary>Used by the repository layer to reconstruct existing rooms from storage.</summary>
    internal static Room Rehydrate(string roomId, string name, string? roomType) => new(roomId, name, roomType);
}
