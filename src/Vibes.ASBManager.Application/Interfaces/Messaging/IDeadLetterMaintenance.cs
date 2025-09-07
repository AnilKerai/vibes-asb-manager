namespace Vibes.ASBManager.Application.Interfaces;

public interface IDeadLetterMaintenance
{
    Task<int> PurgeQueueDeadLetterAsync(string connectionString, string queueName, int maxMessages = 1000, CancellationToken ct = default);
    Task<int> PurgeSubscriptionDeadLetterAsync(string connectionString, string topicName, string subscriptionName, int maxMessages = 1000, CancellationToken ct = default);
    Task<int> ReplayQueueDeadLettersAsync(string connectionString, string queueName, int maxMessages = 50, CancellationToken ct = default);
    Task<bool> RemoveQueueDeadLetterMessageAsync(string connectionString, string queueName, long sequenceNumber, CancellationToken ct = default);
    Task<bool> RemoveSubscriptionDeadLetterMessageAsync(string connectionString, string topicName, string subscriptionName, long sequenceNumber, CancellationToken ct = default);
}
