using Vibes.ASBManager.Application.Models;

namespace Vibes.ASBManager.Application.Interfaces;

public interface IServiceBusAdmin
{
    Task<IReadOnlyList<QueueSummary>> ListQueuesAsync(string connectionString, CancellationToken ct = default);
    Task<IReadOnlyList<TopicSummary>> ListTopicsAsync(string connectionString, CancellationToken ct = default);
    Task CreateQueueAsync(
        string connectionString,
        string queueName,
        bool requiresSession,
        TimeSpan lockDuration,
        int maxDeliveryCount,
        bool enableBatchedOperations,
        TimeSpan? defaultMessageTimeToLive,
        bool deadLetterOnExpiration,
        string? forwardTo,
        string? forwardDeadLetteredMessagesTo,
        CancellationToken ct = default);
    Task DeleteQueueAsync(string connectionString, string queueName, CancellationToken ct = default);
    Task CreateTopicAsync(string connectionString, string topicName, CancellationToken ct = default);
    Task DeleteTopicAsync(string connectionString, string topicName, CancellationToken ct = default);
    Task<IReadOnlyList<SubscriptionSummary>> ListSubscriptionsAsync(string connectionString, string topicName, CancellationToken ct = default);

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

    Task<TopicSettings> GetTopicSettingsAsync(string connectionString, string topicName, CancellationToken ct = default);
    Task UpdateTopicSettingsAsync(string connectionString, string topicName, TimeSpan defaultMessageTimeToLive, CancellationToken ct = default);
    Task UpdateTopicPropertiesAsync(
        string connectionString,
        string topicName,
        bool enableBatchedOperations,
        CancellationToken ct = default);
    Task CreateSubscriptionAsync(string connectionString, string topicName, string subscriptionName, CancellationToken ct = default);
    Task DeleteSubscriptionAsync(string connectionString, string topicName, string subscriptionName, CancellationToken ct = default);

    Task<SubscriptionSettings> GetSubscriptionSettingsAsync(string connectionString, string topicName, string subscriptionName, CancellationToken ct = default);
    Task UpdateSubscriptionSettingsAsync(string connectionString, string topicName, string subscriptionName, TimeSpan defaultMessageTimeToLive, bool deadLetteringOnMessageExpiration, CancellationToken ct = default);
    Task UpdateSubscriptionPropertiesAsync(
        string connectionString,
        string topicName,
        string subscriptionName,
        bool requiresSession,
        TimeSpan lockDuration,
        int maxDeliveryCount,
        bool enableBatchedOperations,
        string? forwardTo,
        string? forwardDeadLetteredMessagesTo,
        CancellationToken ct = default);
    
    Task<IReadOnlyList<SubscriptionRuleInfo>> ListSubscriptionRulesAsync(string connectionString, string topicName, string subscriptionName, CancellationToken ct = default);
    Task CreateSubscriptionSqlRuleAsync(string connectionString, string topicName, string subscriptionName, string ruleName, string sqlExpression, string? sqlAction = null, CancellationToken ct = default);
    Task CreateSubscriptionCorrelationRuleAsync(
        string connectionString,
        string topicName,
        string subscriptionName,
        string ruleName,
        string? correlationId,
        string? subject,
        string? to,
        string? replyTo,
        string? replyToSessionId,
        string? sessionId,
        string? contentType,
        Dictionary<string, string>? applicationProperties = null,
        CancellationToken ct = default);
    Task DeleteSubscriptionRuleAsync(string connectionString, string topicName, string subscriptionName, string ruleName, CancellationToken ct = default);
}
