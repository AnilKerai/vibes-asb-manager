using Vibes.ASBManager.Application.Models;

namespace Vibes.ASBManager.Application.Interfaces.Messaging;

public interface IMessageBrowser
{
    Task<IReadOnlyList<MessagePreview>> PeekQueueAsync(string connectionString, string queueName, int maxMessages = 50, long? fromSequenceNumber = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MessagePreview>> PeekQueueDeadLetterAsync(string connectionString, string queueName, int maxMessages = 50, long? fromSequenceNumber = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MessagePreview>> PeekSubscriptionAsync(string connectionString, string topicName, string subscriptionName, int maxMessages = 50, long? fromSequenceNumber = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MessagePreview>> PeekSubscriptionDeadLetterAsync(string connectionString, string topicName, string subscriptionName, int maxMessages = 50, long? fromSequenceNumber = null, CancellationToken cancellationToken = default);

    Task<MessageDetails?> PeekQueueMessageAsync(string connectionString, string queueName, long sequenceNumber, CancellationToken cancellationToken = default);
    Task<MessageDetails?> PeekQueueDeadLetterMessageAsync(string connectionString, string queueName, long sequenceNumber, CancellationToken cancellationToken = default);
    Task<MessageDetails?> PeekSubscriptionMessageAsync(string connectionString, string topicName, string subscriptionName, long sequenceNumber, CancellationToken cancellationToken = default);
    Task<MessageDetails?> PeekSubscriptionDeadLetterMessageAsync(string connectionString, string topicName, string subscriptionName, long sequenceNumber, CancellationToken cancellationToken = default);
}
