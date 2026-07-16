using System.Text.Json;
using SmartNest.PlatformService.Persistence.Audit;
using SmartNest.PlatformService.Repositories.Audit;

namespace SmartNest.PlatformService.Handlers.Audit;

/// <summary>
/// Shared append logic (Task 7) - deserializes any <c>EventEnvelope&lt;JsonElement&gt;</c>
/// (works for every event type published by every service, since Audit only needs to
/// store the envelope, not interpret the payload) into an
/// <see cref="AuditEntryDocument"/> and appends it atomically via
/// <see cref="IAuditRepository.AppendAsync"/>. Reused by the three Service Bus trigger
/// Functions (one per topic subscription - device-events/audit, home-events/audit,
/// user-events/audit).
/// </summary>
public sealed class AppendAuditLogHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IAuditRepository _repository;

    public AppendAuditLogHandler(IAuditRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task HandleAsync(string messageBody, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(messageBody))
            throw new ArgumentException("Message body is required.", nameof(messageBody));

        using var document = JsonDocument.Parse(messageBody);
        var root = document.RootElement;

        var entry = new AuditEntryDocument
        {
            Id = root.GetProperty("eventId").GetString()!,
            EventId = root.GetProperty("eventId").GetString()!,
            EventType = root.GetProperty("eventType").GetString()!,
            AggregateId = root.GetProperty("aggregateId").GetString()!,
            AggregateType = root.GetProperty("aggregateType").GetString()!,
            OccurredAt = root.GetProperty("occurredAt").GetDateTimeOffset(),
            ActorId = root.GetProperty("actorId").GetString()!,
            HomeId = root.GetProperty("homeId").GetString()!,
            CorrelationId = root.GetProperty("correlationId").GetString()!,
            Payload = root.GetProperty("payload").GetRawText(),
        };

        await _repository.AppendAsync(entry, cancellationToken).ConfigureAwait(false);
    }
}
