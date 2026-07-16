using FluentAssertions;
using SmartNest.PlatformService.Handlers.Audit;
using SmartNest.PlatformService.Persistence.Audit;

namespace SmartNest.PlatformService.Tests.Handlers.Audit;

public class AppendAuditLogHandlerTests
{
    private sealed class FakeAuditRepository : SmartNest.PlatformService.Repositories.Audit.IAuditRepository
    {
        public AuditEntryDocument? LastAppended { get; private set; }

        public Task<int> AppendAsync(AuditEntryDocument entry, CancellationToken cancellationToken = default)
        {
            LastAppended = entry;
            return Task.FromResult(1);
        }

        public Task<IReadOnlyList<AuditEntryDocument>> GetByAggregateAsync(string aggregateId, int fromSequence = 0, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AuditEntryDocument>>(new List<AuditEntryDocument>());

        public Task<IReadOnlyList<string>> GetDistinctHomeIdsAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<string>>(new List<string>());

        public Task<IReadOnlyList<AuditEntryDocument>> GetByHomeAndDateRangeAsync(string homeId, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AuditEntryDocument>>(new List<AuditEntryDocument>());
    }

    [Fact]
    public async Task HandleAsync_ParsesEnvelope_AndAppendsEntry()
    {
        var repository = new FakeAuditRepository();
        var handler = new AppendAuditLogHandler(repository);
        var message = """
        {
            "eventId": "evt-1",
            "eventType": "DeviceStateChanged",
            "aggregateId": "device-1",
            "aggregateType": "Device",
            "occurredAt": "2024-01-15T00:00:00Z",
            "actorId": "user-1",
            "homeId": "home-1",
            "correlationId": "corr-1",
            "payload": { "deviceId": "device-1", "property": "temperature" }
        }
        """;

        await handler.HandleAsync(message);

        repository.LastAppended.Should().NotBeNull();
        repository.LastAppended!.EventType.Should().Be("DeviceStateChanged");
        repository.LastAppended.AggregateId.Should().Be("device-1");
        repository.LastAppended.Payload.Should().Contain("temperature");
    }
}
