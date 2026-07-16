namespace SmartNest.PlatformService.Dtos.Summary;

public sealed record DailySummaryResponse(
    string HomeId,
    string Date,
    IReadOnlyDictionary<string, int> EventCounts,
    int TotalEvents,
    DateTimeOffset GeneratedAt)
{
    public static DailySummaryResponse FromDocument(Persistence.Summary.DailySummaryDocument document) => new(
        HomeId: document.HomeId,
        Date: document.Date,
        EventCounts: document.EventCounts,
        TotalEvents: document.TotalEvents,
        GeneratedAt: document.GeneratedAt);
}
