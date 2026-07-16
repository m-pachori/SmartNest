using SmartNest.Shared.Events;

namespace SmartNest.PlatformService.Events.Media;

/// <summary>
/// Publishes <c>DocumentProcessed</c> (per smartnest-plan.md Task 9's naming) to the
/// <c>device-events</c> topic - media is device-scoped, and Audit already subscribes there.
/// </summary>
public sealed class MediaEventPublisher
{
    public const string TopicName = "device-events";

    private readonly IEventPublisher _eventPublisher;

    public MediaEventPublisher(IEventPublisher eventPublisher)
    {
        _eventPublisher = eventPublisher ?? throw new ArgumentNullException(nameof(eventPublisher));
    }

    public Task PublishProcessedAsync(
        string homeId,
        string deviceId,
        string blobName,
        string contentType,
        long sizeBytes,
        CancellationToken cancellationToken = default)
    {
        var envelope = EventEnvelope<MediaProcessedPayload>.Create(
            eventType: "DocumentProcessed",
            aggregateId: deviceId,
            aggregateType: "Media",
            actorId: "system:media-service",
            homeId: homeId,
            payload: new MediaProcessedPayload(homeId, deviceId, blobName, contentType, sizeBytes));

        return _eventPublisher.PublishAsync(TopicName, envelope, cancellationToken);
    }
}
