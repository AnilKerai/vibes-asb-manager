namespace Vibes.ASBManager.Application.Interfaces.Messaging;

// Implemented by anything that caches a Service Bus client per connection string. Connection
// management calls EvictAsync when a connection is removed (or its connection string changes) so a
// stale client — and its open AMQP connection — doesn't linger until the app shuts down. Inject
// IEnumerable<IServiceBusClientCache> to evict across every cache (data plane + admin).
public interface IServiceBusClientCache
{
    ValueTask EvictAsync(string? connectionString, CancellationToken cancellationToken = default);
}
