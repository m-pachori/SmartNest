using SmartNest.DeviceService.Domain.Events;
using SmartNest.DeviceService.Domain.ValueObjects;

namespace SmartNest.DeviceService.Domain;

/// <summary>
/// Device aggregate root. Enforces invariants for the Device bounded context (see
/// smartnest-plan.md "Bounded Contexts & Aggregates"). Only publishes the three
/// documented domain events: DeviceRegistered, DeviceStateChanged, DeviceRemoved.
/// </summary>
public sealed class Device
{
    private readonly List<IDomainEvent> _domainEvents = new();

    public string DeviceId { get; private set; } = default!;

    public string HomeId { get; private set; } = default!;

    public DeviceMetadata Metadata { get; private set; } = default!;

    public DeviceState? State { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    // For repository/document mapping only.
    private Device()
    {
    }

    public static Device Register(string homeId, DeviceMetadata metadata)
    {
        if (string.IsNullOrWhiteSpace(homeId))
            throw new ArgumentException("HomeId is required.", nameof(homeId));
        ArgumentNullException.ThrowIfNull(metadata);

        var now = DateTimeOffset.UtcNow;
        var device = new Device
        {
            DeviceId = Guid.NewGuid().ToString(),
            HomeId = homeId,
            Metadata = metadata,
            CreatedAt = now,
            UpdatedAt = now,
        };

        device._domainEvents.Add(new DeviceRegisteredDomainEvent(device.DeviceId, device.HomeId));
        return device;
    }

    /// <summary>Reconstructs an existing Device from storage without raising domain events.</summary>
    internal static Device Rehydrate(
        string deviceId,
        string homeId,
        DeviceMetadata metadata,
        DeviceState? state,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt) =>
        new()
        {
            DeviceId = deviceId,
            HomeId = homeId,
            Metadata = metadata,
            State = state,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
        };

    /// <summary>
    /// Records a new observed value for <paramref name="property"/>, raising
    /// <c>DeviceStateChanged</c> with the previous value (if any) and the new value.
    /// </summary>
    public void UpdateState(string property, StateValue value)
    {
        if (string.IsNullOrWhiteSpace(property))
            throw new ArgumentException("Property is required.", nameof(property));
        ArgumentNullException.ThrowIfNull(value);

        var oldValue = State?.Value.ToPayloadString();
        var now = DateTimeOffset.UtcNow;
        State = new DeviceState(property, value, now);
        UpdatedAt = now;

        _domainEvents.Add(new DeviceStateChangedDomainEvent(
            DeviceId, HomeId, property, oldValue, value.ToPayloadString(), value.Unit));
    }

    /// <summary>Raises the DeviceRemoved domain event. Call before the repository deletes the document.</summary>
    public void MarkRemoved()
    {
        _domainEvents.Add(new DeviceRemovedDomainEvent(DeviceId, HomeId));
    }

    public void ClearDomainEvents() => _domainEvents.Clear();
}
