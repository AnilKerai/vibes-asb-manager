namespace Vibes.ASBManager.Application.Interfaces.Messaging;

public interface IDeadLetterMaintenance
{
    Task<int> PurgeQueueDeadLetterAsync(string connectionString, string queueName, int maxMessages = 1000, CancellationToken cancellationToken = default);
    Task<int> PurgeSubscriptionDeadLetterAsync(string connectionString, string topicName, string subscriptionName, int maxMessages = 1000, CancellationToken cancellationToken = default);
    Task<int> ReplayQueueDeadLettersAsync(string connectionString, string queueName, int maxMessages = 50, CancellationToken cancellationToken = default);
    Task<int> ReplaySubscriptionDeadLettersAsync(string connectionString, string topicName, string subscriptionName, int maxMessages = 50, CancellationToken cancellationToken = default);
    Task<bool> RemoveQueueDeadLetterMessageAsync(string connectionString, string queueName, long sequenceNumber, CancellationToken cancellationToken = default);
    Task<bool> RemoveSubscriptionDeadLetterMessageAsync(string connectionString, string topicName, string subscriptionName, long sequenceNumber, CancellationToken cancellationToken = default);
}
