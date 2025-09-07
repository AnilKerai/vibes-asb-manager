using Vibes.ASBManager.Application.Models;

namespace Vibes.ASBManager.Application.Interfaces;

public interface IQueueAdmin
{
    Task<IReadOnlyList<QueueSummary>> ListQueuesAsync(string connectionString, CancellationToken ct = default);
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
        CancellationToken ct = default);
    Task DeleteQueueAsync(string connectionString, string queueName, CancellationToken ct = default);

    Task<QueueSettings> GetQueueSettingsAsync(string connectionString, string queueName, CancellationToken ct = default);
    Task UpdateQueueSettingsAsync(string connectionString, string queueName, TimeSpan defaultMessageTimeToLive, bool deadLetteringOnMessageExpiration, CancellationToken ct = default);
    Task UpdateQueuePropertiesAsync(
        string connectionString,
        string queueName,
        TimeSpan lockDuration,
        int maxDeliveryCount,
        bool enableBatchedOperations,
        string? forwardTo,
        string? forwardDeadLetteredMessagesTo,
        CancellationToken ct = default);
}
