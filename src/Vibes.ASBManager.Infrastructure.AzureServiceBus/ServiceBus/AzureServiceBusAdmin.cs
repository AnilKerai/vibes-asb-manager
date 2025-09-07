using System.Diagnostics.CodeAnalysis;
using Azure;
using Azure.Messaging.ServiceBus.Administration;
using Vibes.ASBManager.Application.Interfaces;
using Vibes.ASBManager.Application.Models;

namespace Vibes.ASBManager.Infrastructure.AzureServiceBus.ServiceBus;

[ExcludeFromCodeCoverage]
public sealed class AzureServiceBusAdmin(
    IRuleFormatter ruleFormatter
) : IServiceBusAdmin
{
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
        CancellationToken ct = default)
    {
        var client = new ServiceBusAdministrationClient(connectionString);
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
        await client.CreateQueueAsync(options, ct).ConfigureAwait(false);
    }

    public async Task DeleteQueueAsync(string connectionString, string queueName, CancellationToken ct = default)
    {
        var client = new ServiceBusAdministrationClient(connectionString);
        await client.DeleteQueueAsync(queueName, ct).ConfigureAwait(false);
    }

    public async Task CreateTopicAsync(string connectionString, string topicName, CancellationToken ct = default)
    {
        var client = new ServiceBusAdministrationClient(connectionString);
        var options = new CreateTopicOptions(topicName);
        await client.CreateTopicAsync(options, ct).ConfigureAwait(false);
    }

    public async Task DeleteTopicAsync(string connectionString, string topicName, CancellationToken ct = default)
    {
        var client = new ServiceBusAdministrationClient(connectionString);
        await client.DeleteTopicAsync(topicName, ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<TopicSummary>> ListTopicsAsync(string connectionString, CancellationToken ct = default)
    {
        var client = new ServiceBusAdministrationClient(connectionString);
        var result = new List<TopicSummary>();
        await foreach (var tp in client.GetTopicsAsync(ct))
        {
            var subCount = 0;
            await foreach (var _ in client.GetSubscriptionsAsync(tp.Name, ct))
            {
                subCount++;
            }
            long scheduled = 0;
            try
            {
                var runtime = await client.GetTopicRuntimePropertiesAsync(tp.Name, ct).ConfigureAwait(false);
                scheduled = (long)runtime.Value.ScheduledMessageCount;
            }
            catch (RequestFailedException)
            {
            }
            result.Add(new TopicSummary { Name = tp.Name, SubscriptionCount = subCount, ScheduledMessageCount = scheduled });
        }
        return result.OrderBy(t => t.Name).ToList();
    }

    public async Task<IReadOnlyList<QueueSummary>> ListQueuesAsync(string connectionString, CancellationToken ct = default)
    {
        var client = new ServiceBusAdministrationClient(connectionString);
        var result = new List<QueueSummary>();
        await foreach (var qrp in client.GetQueuesRuntimePropertiesAsync(ct))
        {
            result.Add(new QueueSummary
            {
                Name = qrp.Name,
                ActiveMessageCount = (long)qrp.ActiveMessageCount,
                DeadLetterMessageCount = (long)qrp.DeadLetterMessageCount
            });
        }
        return result.OrderBy(q => q.Name).ToList();
    }

    public async Task<IReadOnlyList<SubscriptionSummary>> ListSubscriptionsAsync(string connectionString, string topicName, CancellationToken ct = default)
    {
        var client = new ServiceBusAdministrationClient(connectionString);
        var result = new List<SubscriptionSummary>();
        await foreach (var srp in client.GetSubscriptionsRuntimePropertiesAsync(topicName, ct))
        {
            result.Add(new SubscriptionSummary
            {
                TopicName = topicName,
                SubscriptionName = srp.SubscriptionName,
                ActiveMessageCount = (long)srp.ActiveMessageCount,
                DeadLetterMessageCount = (long)srp.DeadLetterMessageCount
            });
        }
        return result.OrderBy(s => s.SubscriptionName).ToList();
    }

    public async Task<QueueSettings> GetQueueSettingsAsync(string connectionString, string queueName, CancellationToken ct = default)
    {
        var client = new ServiceBusAdministrationClient(connectionString);
        var resp = await client.GetQueueAsync(queueName, ct).ConfigureAwait(false);
        var p = resp.Value;
        return new QueueSettings
        {
            Name = p.Name,
            DefaultMessageTimeToLive = p.DefaultMessageTimeToLive,
            DeadLetteringOnMessageExpiration = p.DeadLetteringOnMessageExpiration
        };
    }

    public async Task UpdateQueueSettingsAsync(string connectionString, string queueName, TimeSpan defaultMessageTimeToLive, bool deadLetteringOnMessageExpiration, CancellationToken ct = default)
    {
        var client = new ServiceBusAdministrationClient(connectionString);
        var resp = await client.GetQueueAsync(queueName, ct).ConfigureAwait(false);
        var props = resp.Value;
        props.DefaultMessageTimeToLive = defaultMessageTimeToLive;
        props.DeadLetteringOnMessageExpiration = deadLetteringOnMessageExpiration;
        await client.UpdateQueueAsync(props, ct).ConfigureAwait(false);
    }

    public async Task UpdateQueuePropertiesAsync(
        string connectionString,
        string queueName,
        TimeSpan lockDuration,
        int maxDeliveryCount,
        bool enableBatchedOperations,
        string? forwardTo,
        string? forwardDeadLetteredMessagesTo,
        CancellationToken ct = default)
    {
        var client = new ServiceBusAdministrationClient(connectionString);
        var resp = await client.GetQueueAsync(queueName, ct).ConfigureAwait(false);
        var props = resp.Value;
        props.LockDuration = lockDuration;
        props.MaxDeliveryCount = maxDeliveryCount;
        props.EnableBatchedOperations = enableBatchedOperations;
        props.ForwardTo = string.IsNullOrWhiteSpace(forwardTo) ? null : forwardTo;
        props.ForwardDeadLetteredMessagesTo = string.IsNullOrWhiteSpace(forwardDeadLetteredMessagesTo) ? null : forwardDeadLetteredMessagesTo;
        await client.UpdateQueueAsync(props, ct).ConfigureAwait(false);
    }

    public async Task<TopicSettings> GetTopicSettingsAsync(string connectionString, string topicName, CancellationToken ct = default)
    {
        var client = new ServiceBusAdministrationClient(connectionString);
        var resp = await client.GetTopicAsync(topicName, ct).ConfigureAwait(false);
        var p = resp.Value;
        return new TopicSettings
        {
            Name = p.Name,
            DefaultMessageTimeToLive = p.DefaultMessageTimeToLive
        };
    }

    public async Task UpdateTopicSettingsAsync(string connectionString, string topicName, TimeSpan defaultMessageTimeToLive, CancellationToken ct = default)
    {
        var client = new ServiceBusAdministrationClient(connectionString);
        var resp = await client.GetTopicAsync(topicName, ct).ConfigureAwait(false);
        var props = resp.Value;
        props.DefaultMessageTimeToLive = defaultMessageTimeToLive;
        await client.UpdateTopicAsync(props, ct).ConfigureAwait(false);
    }

    public async Task UpdateTopicPropertiesAsync(
        string connectionString,
        string topicName,
        bool enableBatchedOperations,
        CancellationToken ct = default)
    {
        var client = new ServiceBusAdministrationClient(connectionString);
        var resp = await client.GetTopicAsync(topicName, ct).ConfigureAwait(false);
        var props = resp.Value;
        props.EnableBatchedOperations = enableBatchedOperations;
        await client.UpdateTopicAsync(props, ct).ConfigureAwait(false);
    }

    public async Task CreateSubscriptionAsync(string connectionString, string topicName, string subscriptionName, CancellationToken ct = default)
    {
        var client = new ServiceBusAdministrationClient(connectionString);
        var options = new CreateSubscriptionOptions(topicName, subscriptionName);
        await client.CreateSubscriptionAsync(options, ct).ConfigureAwait(false);
    }

    public async Task DeleteSubscriptionAsync(string connectionString, string topicName, string subscriptionName, CancellationToken ct = default)
    {
        var client = new ServiceBusAdministrationClient(connectionString);
        await client.DeleteSubscriptionAsync(topicName, subscriptionName, ct).ConfigureAwait(false);
    }

    public async Task<SubscriptionSettings> GetSubscriptionSettingsAsync(string connectionString, string topicName, string subscriptionName, CancellationToken ct = default)
    {
        var client = new ServiceBusAdministrationClient(connectionString);
        var resp = await client.GetSubscriptionAsync(topicName, subscriptionName, ct).ConfigureAwait(false);
        var p = resp.Value;
        return new SubscriptionSettings
        {
            TopicName = topicName,
            SubscriptionName = subscriptionName,
            DefaultMessageTimeToLive = p.DefaultMessageTimeToLive,
            DeadLetteringOnMessageExpiration = p.DeadLetteringOnMessageExpiration
        };
    }

    public async Task UpdateSubscriptionSettingsAsync(string connectionString, string topicName, string subscriptionName, TimeSpan defaultMessageTimeToLive, bool deadLetteringOnMessageExpiration, CancellationToken ct = default)
    {
        var client = new ServiceBusAdministrationClient(connectionString);
        var resp = await client.GetSubscriptionAsync(topicName, subscriptionName, ct).ConfigureAwait(false);
        var props = resp.Value;
        props.DefaultMessageTimeToLive = defaultMessageTimeToLive;
        props.DeadLetteringOnMessageExpiration = deadLetteringOnMessageExpiration;
        await client.UpdateSubscriptionAsync(props, ct).ConfigureAwait(false);
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
        CancellationToken ct = default)
    {
        var client = new ServiceBusAdministrationClient(connectionString);
        var resp = await client.GetSubscriptionAsync(topicName, subscriptionName, ct).ConfigureAwait(false);
        var props = resp.Value;
        props.RequiresSession = requiresSession;
        props.LockDuration = lockDuration;
        props.MaxDeliveryCount = maxDeliveryCount;
        props.EnableBatchedOperations = enableBatchedOperations;
        props.ForwardTo = string.IsNullOrWhiteSpace(forwardTo) ? null : forwardTo;
        props.ForwardDeadLetteredMessagesTo = string.IsNullOrWhiteSpace(forwardDeadLetteredMessagesTo) ? null : forwardDeadLetteredMessagesTo;
        await client.UpdateSubscriptionAsync(props, ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<SubscriptionRuleInfo>> ListSubscriptionRulesAsync(string connectionString, string topicName, string subscriptionName, CancellationToken ct = default)
    {
        var client = new ServiceBusAdministrationClient(connectionString);
        var result = new List<SubscriptionRuleInfo>();
        await foreach (var rule in client.GetRulesAsync(topicName, subscriptionName, ct))
        {
            result.Add(new SubscriptionRuleInfo
            {
                Name = rule.Name,
                Filter = ruleFormatter.FormatFilter(rule.Filter),
                Action = ruleFormatter.FormatAction(rule.Action)
            });
        }
        return result.OrderBy(r => r.Name).ToList();
    }

    public async Task CreateSubscriptionSqlRuleAsync(string connectionString, string topicName, string subscriptionName, string ruleName, string sqlExpression, string? sqlAction = null, CancellationToken ct = default)
    {
        var client = new ServiceBusAdministrationClient(connectionString);
        var options = new CreateRuleOptions(ruleName, new SqlRuleFilter(sqlExpression));
        if (!string.IsNullOrWhiteSpace(sqlAction))
        {
            options.Action = new SqlRuleAction(sqlAction);
        }
        await client.CreateRuleAsync(topicName, subscriptionName, options, ct).ConfigureAwait(false);
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
        CancellationToken ct = default)
    {
        var client = new ServiceBusAdministrationClient(connectionString);
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
            foreach (var kv in applicationProperties)
            {
                filter.ApplicationProperties[kv.Key] = kv.Value;
            }
        }
        var options = new CreateRuleOptions(ruleName, filter);
        await client.CreateRuleAsync(topicName, subscriptionName, options, ct).ConfigureAwait(false);
    }

    public async Task DeleteSubscriptionRuleAsync(string connectionString, string topicName, string subscriptionName, string ruleName, CancellationToken ct = default)
    {
        var client = new ServiceBusAdministrationClient(connectionString);
        await client.DeleteRuleAsync(topicName, subscriptionName, ruleName, ct).ConfigureAwait(false);
    }

    
}
