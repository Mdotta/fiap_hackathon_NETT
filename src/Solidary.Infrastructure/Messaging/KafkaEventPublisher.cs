using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Options;
using Solidary.Domain.Abstractions;

namespace Solidary.Infrastructure.Messaging;

public class KafkaEventPublisher : IEventPublisher, IAsyncDisposable
{
    private readonly IProducer<string, string> _producer;

    public KafkaEventPublisher(IOptions<KafkaSettings> options)
    {
        var config = new ProducerConfig
        {
            BootstrapServers = options.Value.BootstrapServers
        };

        _producer = new ProducerBuilder<string, string>(config).Build();
    }

    public async Task PublishAsync<TEvent>(string topic, string key, TEvent @event, CancellationToken cancellationToken) where TEvent : class
    {
        var payload = JsonSerializer.Serialize(@event);

        await _producer.ProduceAsync(topic, new Message<string, string> { Key = key, Value = payload }, cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        _producer.Flush(TimeSpan.FromSeconds(5));
        _producer.Dispose();

        return ValueTask.CompletedTask;
    }
}
