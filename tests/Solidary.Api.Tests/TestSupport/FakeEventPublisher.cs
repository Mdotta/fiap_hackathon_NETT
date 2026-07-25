using Solidary.Domain.Abstractions;

namespace Solidary.Api.Tests.TestSupport;

public class FakeEventPublisher : IEventPublisher
{
    public List<(string Topic, string Key, object Event)> PublishedEvents { get; } = [];

    public Task PublishAsync<TEvent>(string topic, string key, TEvent @event, CancellationToken cancellationToken) where TEvent : class
    {
        PublishedEvents.Add((topic, key, @event));
        return Task.CompletedTask;
    }
}
