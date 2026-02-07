using Vibes.ASBManager.Application.Models;

namespace Vibes.ASBManager.Application.Interfaces.Admin;

public interface IQueueAdmin
{
    Task<IReadOnlyList<QueueSummary>> ListQueuesAsync(string connectionString, CancellationToken cancellationToken = default);
    Task<QueueSummary?> GetQueueRuntimeAsync(string connectionString, string queueName, CancellationToken cancellationToken = default);
    Task CreateQueueAsync(
        string connectionString,
        string queueName,
        bool requiresSession,
        TimeSpan lockDuration,
        int maxDeliveryCount,
        bool enableBatchedOperations,
        TimeSpan? defaultMessageTimeToLive,
        bool deadLetterOnMessageExpiration,
        string? forwardTo,
        string? forwardDeadLetteredMessagesTo,
        CancellationToken cancellationToken = default);
    Task DeleteQueueAsync(string connectionString, string queueName, CancellationToken cancellationToken = default);

    Task<QueueSettings> GetQueueSettingsAsync(string connectionString, string queueName, CancellationToken cancellationToken = default);
    Task UpdateQueueSettingsAsync(string connectionString, string queueName, TimeSpan defaultMessageTimeToLive, bool deadLetteringOnMessageExpiration, CancellationToken cancellationToken = default);
    Task UpdateQueuePropertiesAsync(
        string connectionString,
        string queueName,
        TimeSpan lockDuration,
        int maxDeliveryCount,
        bool enableBatchedOperations,
        string? forwardTo,
        string? forwardDeadLetteredMessagesTo,
        CancellationToken cancellationToken = default);
}
