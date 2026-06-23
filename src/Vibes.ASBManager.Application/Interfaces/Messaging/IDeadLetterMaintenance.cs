using Vibes.ASBManager.Application.Messaging;

namespace Vibes.ASBManager.Application.Interfaces.Messaging;

public interface IDeadLetterMaintenance
{
    // maxMessages is a safety ceiling; the purge drains until empty or that ceiling. progress reports the running total.
    Task<int> PurgeQueueDeadLetterAsync(string connectionString, string queueName, int maxMessages = MessagingDefaults.PurgeCeiling, IProgress<int>? progress = null, CancellationToken cancellationToken = default);
    Task<int> PurgeSubscriptionDeadLetterAsync(string connectionString, string topicName, string subscriptionName, int maxMessages = MessagingDefaults.PurgeCeiling, IProgress<int>? progress = null, CancellationToken cancellationToken = default);
    Task<int> ReplayQueueDeadLettersAsync(string connectionString, string queueName, int maxMessages = 50, CancellationToken cancellationToken = default);
    Task<int> ReplaySubscriptionDeadLettersAsync(string connectionString, string topicName, string subscriptionName, int maxMessages = 50, CancellationToken cancellationToken = default);
    Task<bool> RemoveQueueDeadLetterMessageAsync(string connectionString, string queueName, long sequenceNumber, CancellationToken cancellationToken = default);
    Task<bool> RemoveSubscriptionDeadLetterMessageAsync(string connectionString, string topicName, string subscriptionName, long sequenceNumber, CancellationToken cancellationToken = default);
}
