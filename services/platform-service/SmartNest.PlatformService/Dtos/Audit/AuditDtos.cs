namespace SmartNest.PlatformService.Dtos.Audit;

public sealed record AuditEntryResponse(
    string EventId,
    string EventType,
    string AggregateId,
    string AggregateType,
    DateTimeOffset OccurredAt,
    string ActorId,
    string HomeId,
    string CorrelationId,
    string Payload,
    int SequenceNumber)
{
    public static AuditEntryResponse FromDocument(Persistence.Audit.AuditEntryDocument document) => new(
        EventId: document.EventId,
        EventType: document.EventType,
        AggregateId: document.AggregateId,
        AggregateType: document.AggregateType,
        OccurredAt: document.OccurredAt,
        ActorId: document.ActorId,
        HomeId: document.HomeId,
        CorrelationId: document.CorrelationId,
        Payload: document.Payload,
        SequenceNumber: document.SequenceNumber);
}
