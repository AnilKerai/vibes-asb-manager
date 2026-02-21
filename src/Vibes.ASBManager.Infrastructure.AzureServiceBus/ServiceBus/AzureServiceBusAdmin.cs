using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Azure;
using Azure.Messaging.ServiceBus.Administration;
using Microsoft.Extensions.Logging;
using Vibes.ASBManager.Application.Interfaces.Admin;
using Vibes.ASBManager.Application.Models;

namespace Vibes.ASBManager.Infrastructure.AzureServiceBus.ServiceBus;

[ExcludeFromCodeCoverage]
public sealed class AzureServiceBusAdmin(
    IRuleFormatter ruleFormatter,
    ILogger<AzureServiceBusAdmin> logger
) : IQueueAdmin, ITopicAdmin, ISubscriptionAdmin, ISubscriptionRuleAdmin
{
    private readonly ConcurrentDictionary<string, ServiceBusAdministrationClient> _clients = new(StringComparer.Ordinal);
    private readonly ILogger<AzureServiceBusAdmin> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    private ServiceBusAdministrationClient GetClient(string connectionString)
        => _clients.GetOrAdd(connectionString, static cs => new ServiceBusAdministrationClient(cs));

    public async Task CreateQueueAsync(
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
        CancellationToken cancellationToken = default)
    {
        var client = GetClient(connectionString);
        var options = new CreateQueueOptions(queueName)
        {
            RequiresSession = requiresSession,
            LockDuration = lockDuration,
            MaxDeliveryCount = maxDeliveryCount,
            EnableBatchedOperations = enableBatchedOperations,
            DeadLetteringOnMessageExpiration = deadLetterOnMessageExpiration,
            ForwardTo = string.IsNullOrWhiteSpace(forwardTo) ? null : forwardTo,
            ForwardDeadLetteredMessagesTo = string.IsNullOrWhiteSpace(forwardDeadLetteredMessagesTo) ? null : forwardDeadLetteredMessagesTo
        };
        if (defaultMessageTimeToLive.HasValue)
        {
            options.DefaultMessageTimeToLive = defaultMessageTimeToLive.Value;
        }
        await client.CreateQueueAsync(options, cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteQueueAsync(string connectionString, string queueName, CancellationToken cancellationToken = default)
    {
        var client = GetClient(connectionString);
        await client.DeleteQueueAsync(queueName, cancellationToken).ConfigureAwait(false);
    }

    public async Task CreateTopicAsync(string connectionString, string topicName, CancellationToken cancellationToken = default)
    {
        var client = GetClient(connectionString);
        var options = new CreateTopicOptions(topicName);
        await client.CreateTopicAsync(options, cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteTopicAsync(string connectionString, string topicName, CancellationToken cancellationToken = default)
    {
        var client = GetClient(connectionString);
        await client.DeleteTopicAsync(topicName, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<TopicSummary>> ListTopicsAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        var client = GetClient(connectionString);
        var topicSummaries = new List<TopicSummary>();
        await foreach (var topic in client.GetTopicsAsync(cancellationToken))
        {
            long scheduledMessageCount = 0;
            try
            {
                var topicRuntimeResponse = await client.GetTopicRuntimePropertiesAsync(topic.Name, cancellationToken).ConfigureAwait(false);
                scheduledMessageCount = topicRuntimeResponse.Value.ScheduledMessageCount;
            }
            catch (RequestFailedException)
            {
                _logger.LogWarning("Failed to load scheduled message count for topic {TopicName}", topic.Name);
            }
            topicSummaries.Add(new TopicSummary { Name = topic.Name, SubscriptionCount = 0, ScheduledMessageCount = scheduledMessageCount });
        }
        return topicSummaries.OrderBy(topic => topic.Name).ToList();
    }

    public async Task<IReadOnlyList<QueueSummary>> ListQueuesAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        var client = GetClient(connectionString);
        var queueSummaries = new List<QueueSummary>();
        await foreach (var queueRuntimeProperties in client.GetQueuesRuntimePropertiesAsync(cancellationToken))
        {
            queueSummaries.Add(new QueueSummary
            {
                Name = queueRuntimeProperties.Name,
                ActiveMessageCount = queueRuntimeProperties.ActiveMessageCount,
                DeadLetterMessageCount = queueRuntimeProperties.DeadLetterMessageCount
            });
        }
        return queueSummaries.OrderBy(queue => queue.Name).ToList();
    }

    public async Task<QueueSummary?> GetQueueRuntimeAsync(string connectionString, string queueName, CancellationToken cancellationToken = default)
    {
        var client = GetClient(connectionString);
        try
        {
            var response = await client.GetQueueRuntimePropertiesAsync(queueName, cancellationToken).ConfigureAwait(false);
            return new QueueSummary
            {
                Name = response.Value.Name,
                ActiveMessageCount = response.Value.ActiveMessageCount,
                DeadLetterMessageCount = response.Value.DeadLetterMessageCount
            };
        }
        catch (RequestFailedException)
        {
            _logger.LogWarning("Failed to load runtime properties for queue {QueueName}", queueName);
            return null;
        }
    }

    public async Task<IReadOnlyList<SubscriptionSummary>> ListSubscriptionsAsync(string connectionString, string topicName, CancellationToken cancellationToken = default)
    {
        var client = GetClient(connectionString);
        var subscriptionSummaries = new List<SubscriptionSummary>();
        await foreach (var subscriptionRuntimeProperties in client.GetSubscriptionsRuntimePropertiesAsync(topicName, cancellationToken))
        {
            subscriptionSummaries.Add(new SubscriptionSummary
            {
                TopicName = topicName,
                SubscriptionName = subscriptionRuntimeProperties.SubscriptionName,
                ActiveMessageCount = subscriptionRuntimeProperties.ActiveMessageCount,
                DeadLetterMessageCount = subscriptionRuntimeProperties.DeadLetterMessageCount
            });
        }
        return subscriptionSummaries.OrderBy(subscription => subscription.SubscriptionName).ToList();
    }

    public async Task<SubscriptionSummary?> GetSubscriptionRuntimeAsync(string connectionString, string topicName, string subscriptionName, CancellationToken cancellationToken = default)
    {
        var client = GetClient(connectionString);
        try
        {
            var response = await client.GetSubscriptionRuntimePropertiesAsync(topicName, subscriptionName, cancellationToken).ConfigureAwait(false);
            return new SubscriptionSummary
            {
                TopicName = topicName,
                SubscriptionName = response.Value.SubscriptionName,
                ActiveMessageCount = response.Value.ActiveMessageCount,
                DeadLetterMessageCount = response.Value.DeadLetterMessageCount
            };
        }
        catch (RequestFailedException)
        {
            _logger.LogWarning("Failed to load runtime properties for subscription {TopicName}/{SubscriptionName}", topicName, subscriptionName);
            return null;
        }
    }

    public async Task<QueueSettings> GetQueueSettingsAsync(string connectionString, string queueName, CancellationToken cancellationToken = default)
    {
        var client = GetClient(connectionString);
        var queueResponse = await client.GetQueueAsync(queueName, cancellationToken).ConfigureAwait(false);
        var queueProperties = queueResponse.Value;
        return new QueueSettings
        {
            Name = queueProperties.Name,
            DefaultMessageTimeToLive = queueProperties.DefaultMessageTimeToLive,
            DeadLetteringOnMessageExpiration = queueProperties.DeadLetteringOnMessageExpiration,
            LockDuration = queueProperties.LockDuration,
            MaxDeliveryCount = queueProperties.MaxDeliveryCount,
            EnableBatchedOperations = queueProperties.EnableBatchedOperations,
            ForwardTo = queueProperties.ForwardTo,
            ForwardDeadLetteredMessagesTo = queueProperties.ForwardDeadLetteredMessagesTo
        };
    }

    public async Task UpdateQueueSettingsAsync(string connectionString, string queueName, TimeSpan defaultMessageTimeToLive, bool deadLetteringOnMessageExpiration, CancellationToken cancellationToken = default)
    {
        var client = GetClient(connectionString);
        var queueResponse = await client.GetQueueAsync(queueName, cancellationToken).ConfigureAwait(false);
        var queueProperties = queueResponse.Value;
        queueProperties.DefaultMessageTimeToLive = defaultMessageTimeToLive;
        queueProperties.DeadLetteringOnMessageExpiration = deadLetteringOnMessageExpiration;
        await client.UpdateQueueAsync(queueProperties, cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateQueuePropertiesAsync(
        string connectionString,
        string queueName,
        TimeSpan lockDuration,
        int maxDeliveryCount,
        bool enableBatchedOperations,
        string? forwardTo,
        string? forwardDeadLetteredMessagesTo,
        CancellationToken cancellationToken = default)
    {
        var client = GetClient(connectionString);
        var queueResponse = await client.GetQueueAsync(queueName, cancellationToken).ConfigureAwait(false);
        var queueProperties = queueResponse.Value;
        queueProperties.LockDuration = lockDuration;
        queueProperties.MaxDeliveryCount = maxDeliveryCount;
        queueProperties.EnableBatchedOperations = enableBatchedOperations;
        queueProperties.ForwardTo = string.IsNullOrWhiteSpace(forwardTo) ? null : forwardTo;
        queueProperties.ForwardDeadLetteredMessagesTo = string.IsNullOrWhiteSpace(forwardDeadLetteredMessagesTo) ? null : forwardDeadLetteredMessagesTo;
        await client.UpdateQueueAsync(queueProperties, cancellationToken).ConfigureAwait(false);
    }

    public async Task<TopicSettings> GetTopicSettingsAsync(string connectionString, string topicName, CancellationToken cancellationToken = default)
    {
        var client = GetClient(connectionString);
        var topicResponse = await client.GetTopicAsync(topicName, cancellationToken).ConfigureAwait(false);
        var topicProperties = topicResponse.Value;
        return new TopicSettings
        {
            Name = topicProperties.Name,
            DefaultMessageTimeToLive = topicProperties.DefaultMessageTimeToLive,
            EnableBatchedOperations = topicProperties.EnableBatchedOperations
        };
    }

    public async Task UpdateTopicSettingsAsync(string connectionString, string topicName, TimeSpan defaultMessageTimeToLive, CancellationToken cancellationToken = default)
    {
        var client = GetClient(connectionString);
        var topicResponse = await client.GetTopicAsync(topicName, cancellationToken).ConfigureAwait(false);
        var topicProperties = topicResponse.Value;
        topicProperties.DefaultMessageTimeToLive = defaultMessageTimeToLive;
        await client.UpdateTopicAsync(topicProperties, cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateTopicPropertiesAsync(
        string connectionString,
        string topicName,
        bool enableBatchedOperations,
        CancellationToken cancellationToken = default)
    {
        var client = GetClient(connectionString);
        var topicResponse = await client.GetTopicAsync(topicName, cancellationToken).ConfigureAwait(false);
        var topicProperties = topicResponse.Value;
        topicProperties.EnableBatchedOperations = enableBatchedOperations;
        await client.UpdateTopicAsync(topicProperties, cancellationToken).ConfigureAwait(false);
    }

    public async Task CreateSubscriptionAsync(string connectionString, string topicName, string subscriptionName, CancellationToken cancellationToken = default)
    {
        var client = GetClient(connectionString);
        var options = new CreateSubscriptionOptions(topicName, subscriptionName);
        await client.CreateSubscriptionAsync(options, cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteSubscriptionAsync(string connectionString, string topicName, string subscriptionName, CancellationToken cancellationToken = default)
    {
        var client = GetClient(connectionString);
        await client.DeleteSubscriptionAsync(topicName, subscriptionName, cancellationToken).ConfigureAwait(false);
    }

    public async Task<SubscriptionSettings> GetSubscriptionSettingsAsync(string connectionString, string topicName, string subscriptionName, CancellationToken cancellationToken = default)
    {
        var client = GetClient(connectionString);
        var subscriptionResponse = await client.GetSubscriptionAsync(topicName, subscriptionName, cancellationToken).ConfigureAwait(false);
        var subscriptionProperties = subscriptionResponse.Value;
        return new SubscriptionSettings
        {
            TopicName = topicName,
            SubscriptionName = subscriptionName,
            DefaultMessageTimeToLive = subscriptionProperties.DefaultMessageTimeToLive,
            DeadLetteringOnMessageExpiration = subscriptionProperties.DeadLetteringOnMessageExpiration,
            RequiresSession = subscriptionProperties.RequiresSession,
            LockDuration = subscriptionProperties.LockDuration,
            MaxDeliveryCount = subscriptionProperties.MaxDeliveryCount,
            EnableBatchedOperations = subscriptionProperties.EnableBatchedOperations,
            ForwardTo = subscriptionProperties.ForwardTo,
            ForwardDeadLetteredMessagesTo = subscriptionProperties.ForwardDeadLetteredMessagesTo
        };
    }

    public async Task UpdateSubscriptionSettingsAsync(string connectionString, string topicName, string subscriptionName, TimeSpan defaultMessageTimeToLive, bool deadLetteringOnMessageExpiration, CancellationToken cancellationToken = default)
    {
        var client = GetClient(connectionString);
        var subscriptionResponse = await client.GetSubscriptionAsync(topicName, subscriptionName, cancellationToken).ConfigureAwait(false);
        var subscriptionProperties = subscriptionResponse.Value;
        subscriptionProperties.DefaultMessageTimeToLive = defaultMessageTimeToLive;
        subscriptionProperties.DeadLetteringOnMessageExpiration = deadLetteringOnMessageExpiration;
        await client.UpdateSubscriptionAsync(subscriptionProperties, cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateSubscriptionPropertiesAsync(
        string connectionString,
        string topicName,
        string subscriptionName,
        bool requiresSession,
        TimeSpan lockDuration,
        int maxDeliveryCount,
        bool enableBatchedOperations,
        string? forwardTo,
        string? forwardDeadLetteredMessagesTo,
        CancellationToken cancellationToken = default)
    {
        var client = GetClient(connectionString);
        var subscriptionResponse = await client.GetSubscriptionAsync(topicName, subscriptionName, cancellationToken).ConfigureAwait(false);
        var subscriptionProperties = subscriptionResponse.Value;
        subscriptionProperties.RequiresSession = requiresSession;
        subscriptionProperties.LockDuration = lockDuration;
        subscriptionProperties.MaxDeliveryCount = maxDeliveryCount;
        subscriptionProperties.EnableBatchedOperations = enableBatchedOperations;
        subscriptionProperties.ForwardTo = string.IsNullOrWhiteSpace(forwardTo) ? null : forwardTo;
        subscriptionProperties.ForwardDeadLetteredMessagesTo = string.IsNullOrWhiteSpace(forwardDeadLetteredMessagesTo) ? null : forwardDeadLetteredMessagesTo;
        await client.UpdateSubscriptionAsync(subscriptionProperties, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<SubscriptionRuleInfo>> ListSubscriptionRulesAsync(string connectionString, string topicName, string subscriptionName, CancellationToken cancellationToken = default)
    {
        var client = GetClient(connectionString);
        var subscriptionRules = new List<SubscriptionRuleInfo>();
        await foreach (var rule in client.GetRulesAsync(topicName, subscriptionName, cancellationToken))
        {
            subscriptionRules.Add(new SubscriptionRuleInfo
            {
                Name = rule.Name,
                Filter = ruleFormatter.FormatFilter(rule.Filter),
                Action = ruleFormatter.FormatAction(rule.Action)
            });
        }
        return subscriptionRules.OrderBy(rule => rule.Name).ToList();
    }

    public async Task CreateSubscriptionSqlRuleAsync(string connectionString, string topicName, string subscriptionName, string ruleName, string sqlExpression, string? sqlAction = null, CancellationToken cancellationToken = default)
    {
        var client = GetClient(connectionString);
        var options = new CreateRuleOptions(ruleName, new SqlRuleFilter(sqlExpression));
        if (!string.IsNullOrWhiteSpace(sqlAction))
        {
            options.Action = new SqlRuleAction(sqlAction);
        }
        await client.CreateRuleAsync(topicName, subscriptionName, options, cancellationToken).ConfigureAwait(false);
    }

    public async Task CreateSubscriptionCorrelationRuleAsync(
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
        CancellationToken cancellationToken = default)
    {
        var client = GetClient(connectionString);
        var filter = new CorrelationRuleFilter
        {
            CorrelationId = correlationId,
            Subject = subject,
            To = to,
            ReplyTo = replyTo,
            ReplyToSessionId = replyToSessionId,
            SessionId = sessionId,
            ContentType = contentType
        };
        if (applicationProperties is not null)
        {
            foreach (var applicationProperty in applicationProperties)
            {
                filter.ApplicationProperties[applicationProperty.Key] = applicationProperty.Value;
            }
        }
        var options = new CreateRuleOptions(ruleName, filter);
        await client.CreateRuleAsync(topicName, subscriptionName, options, cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteSubscriptionRuleAsync(string connectionString, string topicName, string subscriptionName, string ruleName, CancellationToken cancellationToken = default)
    {
        var client = GetClient(connectionString);
        await client.DeleteRuleAsync(topicName, subscriptionName, ruleName, cancellationToken).ConfigureAwait(false);
    }

}
