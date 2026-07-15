using System.Collections.Concurrent;
using System.Text.Json;
using Azure.Messaging.ServiceBus;

namespace SmartNest.Shared.Events;

/// <summary>
/// <see cref="IEventPublisher"/> implementation backed by Azure Service Bus.
/// Register as a singleton in DI — <see cref="ServiceBusSender"/> instances are cached
/// per topic and reused for the lifetime of this publisher.
/// </summary>
public sealed class ServiceBusEventPublisher : IEventPublisher, IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ServiceBusClient _client;
    private readonly ConcurrentDictionary<string, ServiceBusSender> _senders = new();

    public ServiceBusEventPublisher(ServiceBusClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    public async Task PublishAsync<TPayload>(
        string topicName,
        EventEnvelope<TPayload> envelope,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(topicName))
            throw new ArgumentException("Topic name is required.", nameof(topicName));
        ArgumentNullException.ThrowIfNull(envelope);

        var sender = _senders.GetOrAdd(topicName, _client.CreateSender);

        var body = JsonSerializer.SerializeToUtf8Bytes(envelope, JsonOptions);
        var message = new ServiceBusMessage(body)
        {
            MessageId = envelope.EventId,
            CorrelationId = envelope.CorrelationId,
            ContentType = "application/json",
            Subject = envelope.EventType,
        };
        message.ApplicationProperties["aggregateType"] = envelope.AggregateType;
        message.ApplicationProperties["homeId"] = envelope.HomeId;

        await sender.SendMessageAsync(message, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var sender in _senders.Values)
        {
            await sender.DisposeAsync().ConfigureAwait(false);
        }

        await _client.DisposeAsync().ConfigureAwait(false);
    }
}
