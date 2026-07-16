using FluentAssertions;
using Moq;
using SmartNest.PlatformService.Events.Summary;
using SmartNest.PlatformService.Handlers.Summary;
using SmartNest.PlatformService.Persistence.Audit;
using SmartNest.PlatformService.Persistence.Summary;
using SmartNest.PlatformService.Repositories.Audit;
using SmartNest.PlatformService.Repositories.Summary;
using SmartNest.Shared.Events;

namespace SmartNest.PlatformService.Tests.Handlers.Summary;

public class GenerateDailySummaryHandlerTests
{
    [Fact]
    public async Task RunAsync_AggregatesEventCountsPerHome_AndUpsertsSummary()
    {
        var asOf = new DateTimeOffset(2024, 1, 16, 0, 0, 0, TimeSpan.Zero);
        var from = new DateTimeOffset(2024, 1, 15, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2024, 1, 16, 0, 0, 0, TimeSpan.Zero);

        var auditRepository = new Mock<IAuditRepository>();
        auditRepository.Setup(r => r.GetDistinctHomeIdsAsync(from, to, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "home-1" });
        auditRepository.Setup(r => r.GetByHomeAndDateRangeAsync("home-1", from, to, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AuditEntryDocument>
            {
                new() { EventType = "DeviceStateChanged", HomeId = "home-1" },
                new() { EventType = "DeviceStateChanged", HomeId = "home-1" },
                new() { EventType = "AlertRaised", HomeId = "home-1" },
            });

        DailySummaryDocument? upserted = null;
        var summaryRepository = new Mock<ISummaryRepository>();
        summaryRepository.Setup(r => r.UpsertAsync(It.IsAny<DailySummaryDocument>(), It.IsAny<CancellationToken>()))
            .Callback<DailySummaryDocument, CancellationToken>((doc, _) => upserted = doc)
            .Returns(Task.CompletedTask);

        var eventPublisher = new Mock<IEventPublisher>();
        var handler = new GenerateDailySummaryHandler(auditRepository.Object, summaryRepository.Object, new SummaryEventPublisher(eventPublisher.Object));

        await handler.RunAsync(asOf);

        upserted.Should().NotBeNull();
        upserted!.HomeId.Should().Be("home-1");
        upserted.TotalEvents.Should().Be(3);
        upserted.EventCounts["DeviceStateChanged"].Should().Be(2);
        upserted.EventCounts["AlertRaised"].Should().Be(1);
        upserted.Id.Should().Be("home-1_2024-01-15");

        eventPublisher.Verify(
            p => p.PublishAsync("home-events", It.Is<EventEnvelope<SummaryGeneratedPayload>>(e => e.Payload.TotalEvents == 3), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
