using Vibes.ASBManager.Application.Models;

namespace Vibes.ASBManager.Application.Interfaces.Messaging;

public interface IMessageBrowser
{
    Task<IReadOnlyList<MessagePreview>> PeekQueueAsync(string connectionString, string queueName, int maxMessages = 50, long? fromSequenceNumber = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MessagePreview>> PeekQueueDeadLetterAsync(string connectionString, string queueName, int maxMessages = 50, long? fromSequenceNumber = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MessagePreview>> PeekSubscriptionAsync(string connectionString, string topicName, string subscriptionName, int maxMessages = 50, long? fromSequenceNumber = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MessagePreview>> PeekSubscriptionDeadLetterAsync(string connectionString, string topicName, string subscriptionName, int maxMessages = 50, long? fromSequenceNumber = null, CancellationToken cancellationToken = default);

    // Snapshot peeks: page through up to <paramref name="target"/> messages behind a single
    // long-lived receiver (no per-page receiver churn), tolerating the short/empty batches a
    // peek can return. Returns messages ordered by sequence number.
    Task<IReadOnlyList<MessagePreview>> PeekQueueSnapshotAsync(string connectionString, string queueName, int target, int fetchSize = 50, int maxEmptyPeeks = 3, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MessagePreview>> PeekQueueDeadLetterSnapshotAsync(string connectionString, string queueName, int target, int fetchSize = 50, int maxEmptyPeeks = 3, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MessagePreview>> PeekSubscriptionSnapshotAsync(string connectionString, string topicName, string subscriptionName, int target, int fetchSize = 50, int maxEmptyPeeks = 3, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MessagePreview>> PeekSubscriptionDeadLetterSnapshotAsync(string connectionString, string topicName, string subscriptionName, int target, int fetchSize = 50, int maxEmptyPeeks = 3, CancellationToken cancellationToken = default);

    Task<MessageDetails?> PeekQueueMessageAsync(string connectionString, string queueName, long sequenceNumber, CancellationToken cancellationToken = default);
    Task<MessageDetails?> PeekQueueDeadLetterMessageAsync(string connectionString, string queueName, long sequenceNumber, CancellationToken cancellationToken = default);
    Task<MessageDetails?> PeekSubscriptionMessageAsync(string connectionString, string topicName, string subscriptionName, long sequenceNumber, CancellationToken cancellationToken = default);
    Task<MessageDetails?> PeekSubscriptionDeadLetterMessageAsync(string connectionString, string topicName, string subscriptionName, long sequenceNumber, CancellationToken cancellationToken = default);
}
