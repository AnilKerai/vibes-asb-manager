using Vibes.ASBManager.Application.Models;

namespace Vibes.ASBManager.Application.Interfaces;

public interface ISubscriptionAdmin
{
    Task<IReadOnlyList<SubscriptionSummary>> ListSubscriptionsAsync(string connectionString, string topicName, CancellationToken cancellationToken = default);
    Task CreateSubscriptionAsync(string connectionString, string topicName, string subscriptionName, CancellationToken cancellationToken = default);
    Task DeleteSubscriptionAsync(string connectionString, string topicName, string subscriptionName, CancellationToken cancellationToken = default);

    Task<SubscriptionSettings> GetSubscriptionSettingsAsync(string connectionString, string topicName, string subscriptionName, CancellationToken cancellationToken = default);
    Task UpdateSubscriptionSettingsAsync(string connectionString, string topicName, string subscriptionName, TimeSpan defaultMessageTimeToLive, bool deadLetteringOnMessageExpiration, CancellationToken cancellationToken = default);
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
        CancellationToken cancellationToken = default);
}
