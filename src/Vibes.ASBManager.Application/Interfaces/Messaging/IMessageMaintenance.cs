using Vibes.ASBManager.Application.Messaging;

namespace Vibes.ASBManager.Application.Interfaces.Messaging;

public interface IMessageMaintenance
{
    // maxMessages is a safety ceiling; the purge drains until empty or that ceiling. progress reports the running total.
    Task<int> PurgeQueueAsync(string connectionString, string queueName, int maxMessages = MessagingDefaults.PurgeCeiling, IProgress<int>? progress = null, CancellationToken cancellationToken = default);
    Task<int> PurgeSubscriptionAsync(string connectionString, string topicName, string subscriptionName, int maxMessages = MessagingDefaults.PurgeCeiling, IProgress<int>? progress = null, CancellationToken cancellationToken = default);
}
