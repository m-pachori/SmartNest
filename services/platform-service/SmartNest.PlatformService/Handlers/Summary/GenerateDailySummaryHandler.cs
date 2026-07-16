using SmartNest.PlatformService.Events.Summary;
using SmartNest.PlatformService.Persistence.Summary;
using SmartNest.PlatformService.Repositories.Audit;
using SmartNest.PlatformService.Repositories.Summary;

namespace SmartNest.PlatformService.Handlers.Summary;

/// <summary>
/// Timer-triggered logic (Task 8) - for the previous UTC calendar day, discovers every
/// home with audit activity, aggregates event counts by <c>eventType</c> from the
/// <c>audit-log</c> container, upserts a <see cref="DailySummaryDocument"/> per home
/// (idempotent - keyed by <c>{homeId}_{yyyy-MM-dd}</c>), and publishes
/// <c>SummaryGenerated</c> per home.
/// </summary>
public sealed class GenerateDailySummaryHandler
{
    private readonly IAuditRepository _auditRepository;
    private readonly ISummaryRepository _summaryRepository;
    private readonly SummaryEventPublisher _eventPublisher;

    public GenerateDailySummaryHandler(IAuditRepository auditRepository, ISummaryRepository summaryRepository, SummaryEventPublisher eventPublisher)
    {
        _auditRepository = auditRepository ?? throw new ArgumentNullException(nameof(auditRepository));
        _summaryRepository = summaryRepository ?? throw new ArgumentNullException(nameof(summaryRepository));
        _eventPublisher = eventPublisher ?? throw new ArgumentNullException(nameof(eventPublisher));
    }

    /// <summary>Runs the nightly job for the UTC day preceding <paramref name="asOfUtc"/> (defaults to now).</summary>
    public async Task RunAsync(DateTimeOffset? asOfUtc = null, CancellationToken cancellationToken = default)
    {
        var today = (asOfUtc ?? DateTimeOffset.UtcNow).UtcDateTime.Date;
        var from = new DateTimeOffset(today.AddDays(-1), TimeSpan.Zero);
        var to = new DateTimeOffset(today, TimeSpan.Zero);
        var dateLabel = from.ToString("yyyy-MM-dd");

        var homeIds = await _auditRepository.GetDistinctHomeIdsAsync(from, to, cancellationToken).ConfigureAwait(false);

        foreach (var homeId in homeIds)
        {
            var entries = await _auditRepository.GetByHomeAndDateRangeAsync(homeId, from, to, cancellationToken).ConfigureAwait(false);

            var eventCounts = entries
                .GroupBy(e => e.EventType)
                .ToDictionary(g => g.Key, g => g.Count());

            var summary = new DailySummaryDocument
            {
                Id = $"{homeId}_{dateLabel}",
                HomeId = homeId,
                Date = dateLabel,
                EventCounts = eventCounts,
                TotalEvents = entries.Count,
                GeneratedAt = DateTimeOffset.UtcNow,
            };

            await _summaryRepository.UpsertAsync(summary, cancellationToken).ConfigureAwait(false);
            await _eventPublisher.PublishGeneratedAsync(homeId, dateLabel, entries.Count, cancellationToken).ConfigureAwait(false);
        }
    }
}
