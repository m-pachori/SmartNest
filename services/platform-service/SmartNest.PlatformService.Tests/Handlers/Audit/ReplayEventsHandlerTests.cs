using FluentAssertions;
using Moq;
using SmartNest.PlatformService.Handlers.Audit;
using SmartNest.PlatformService.Persistence.Audit;
using SmartNest.PlatformService.Repositories.Audit;
using SmartNest.Shared.Security;

namespace SmartNest.PlatformService.Tests.Handlers.Audit;

public class ReplayEventsHandlerTests
{
    private static CurrentUser MakeUser(params string[] roles) => new() { UserId = "user-1", Roles = roles };

    [Fact]
    public async Task HandleAsync_ReturnsOrderedEntries_WhenCallerIsOwner()
    {
        var repository = new Mock<IAuditRepository>();
        repository.Setup(r => r.GetByAggregateAsync("device-1", 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AuditEntryDocument>
            {
                new() { Id = "e1", AggregateId = "device-1", EventType = "DeviceRegistered", SequenceNumber = 1, EventId = "e1", AggregateType = "Device", ActorId = "a", HomeId = "h", CorrelationId = "c", Payload = "{}" },
            });
        var handler = new ReplayEventsHandler(repository.Object);

        var result = await handler.HandleAsync(MakeUser("SmartNest.Owner"), "device-1");

        result.Should().ContainSingle();
        result[0].SequenceNumber.Should().Be(1);
    }

    [Fact]
    public async Task HandleAsync_Throws_WhenCallerIsNotOwner()
    {
        var repository = new Mock<IAuditRepository>();
        var handler = new ReplayEventsHandler(repository.Object);

        var act = () => handler.HandleAsync(MakeUser("SmartNest.Technician"), "device-1");

        await act.Should().ThrowAsync<ForbiddenException>();
    }
}
