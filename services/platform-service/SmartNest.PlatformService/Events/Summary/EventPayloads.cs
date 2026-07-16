namespace SmartNest.PlatformService.Events.Summary;

public sealed record SummaryGeneratedPayload(string HomeId, string Date, int TotalEvents);
