namespace Vibes.ASBManager.Application.Interfaces;

public interface IMessageMaintenance
{
    Task<int> PurgeQueueAsync(string connectionString, string queueName, int maxMessages = 1000, CancellationToken cancellationToken = default);
    Task<int> PurgeSubscriptionAsync(string connectionString, string topicName, string subscriptionName, int maxMessages = 1000, CancellationToken cancellationToken = default);
}
