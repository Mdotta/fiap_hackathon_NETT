namespace Solidary.Domain.Abstractions;

public interface IEventPublisher
{
    Task PublishAsync<TEvent>(string topic, string key, TEvent @event, CancellationToken cancellationToken) where TEvent : class;
}
