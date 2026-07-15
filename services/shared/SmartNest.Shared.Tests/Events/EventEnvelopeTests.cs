using System.Text.Json;
using FluentAssertions;
using SmartNest.Shared.Events;
using Xunit;

namespace SmartNest.Shared.Tests.Events;

public class EventEnvelopeTests
{
    private sealed record TestPayload(string DeviceId, string HomeId);

    [Fact]
    public void Create_PopulatesGeneratedIdsAndTimestamp()
    {
        var payload = new TestPayload("device-1", "home-1");

        var envelope = EventEnvelope<TestPayload>.Create(
            eventType: "HomeCreated",
            aggregateId: "home-1",
            aggregateType: "Home",
            actorId: "user-1",
            homeId: "home-1",
            payload: payload);

        envelope.EventId.Should().NotBeNullOrWhiteSpace();
        Guid.TryParse(envelope.EventId, out _).Should().BeTrue();
        envelope.CorrelationId.Should().NotBeNullOrWhiteSpace();
        Guid.TryParse(envelope.CorrelationId, out _).Should().BeTrue();
        envelope.OccurredAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
        envelope.EventType.Should().Be("HomeCreated");
        envelope.AggregateId.Should().Be("home-1");
        envelope.AggregateType.Should().Be("Home");
        envelope.ActorId.Should().Be("user-1");
        envelope.HomeId.Should().Be("home-1");
        envelope.Payload.Should().Be(payload);
    }

    [Fact]
    public void Create_UsesSuppliedCorrelationId_WhenProvided()
    {
        var envelope = EventEnvelope<TestPayload>.Create(
            eventType: "HomeCreated",
            aggregateId: "home-1",
            aggregateType: "Home",
            actorId: "user-1",
            homeId: "home-1",
            payload: new TestPayload("device-1", "home-1"),
            correlationId: "fixed-correlation-id");

        envelope.CorrelationId.Should().Be("fixed-correlation-id");
    }

    [Theory]
    [InlineData("", "home-1", "Home", "user-1", "home-1")]
    [InlineData("HomeCreated", "", "Home", "user-1", "home-1")]
    [InlineData("HomeCreated", "home-1", "", "user-1", "home-1")]
    [InlineData("HomeCreated", "home-1", "Home", "", "home-1")]
    [InlineData("HomeCreated", "home-1", "Home", "user-1", "")]
    public void Create_Throws_WhenRequiredStringIsMissing(
        string eventType, string aggregateId, string aggregateType, string actorId, string homeId)
    {
        var act = () => EventEnvelope<TestPayload>.Create(
            eventType, aggregateId, aggregateType, actorId, homeId, new TestPayload("d", "h"));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Envelope_RoundTripsThroughJsonSerialization()
    {
        var envelope = EventEnvelope<TestPayload>.Create(
            "HomeCreated", "home-1", "Home", "user-1", "home-1", new TestPayload("device-1", "home-1"));

        var json = JsonSerializer.Serialize(envelope);
        var deserialized = JsonSerializer.Deserialize<EventEnvelope<TestPayload>>(json);

        deserialized.Should().BeEquivalentTo(envelope);
    }
}
