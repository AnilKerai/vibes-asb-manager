using Azure.Messaging.ServiceBus;
using Vibes.ASBManager.Application.Interfaces;
using Vibes.ASBManager.Application.Models;

namespace Vibes.ASBManager.Infrastructure.AzureServiceBus.ServiceBus;

public sealed class AzureServiceBusMessaging : IServiceBusMessaging
{
    public async Task<IReadOnlyList<MessagePreview>> PeekQueueAsync(string connectionString, string queueName, int maxMessages = 50, long? fromSequenceNumber = null, CancellationToken ct = default)
    {
        await using var client = new ServiceBusClient(connectionString);
        await using var receiver = client.CreateReceiver(queueName);

        var messages = await receiver.PeekMessagesAsync(maxMessages, fromSequenceNumber, ct).ConfigureAwait(false);

        return messages
            .Select(m => new MessagePreview
            {
                SequenceNumber = m.SequenceNumber,
                EnqueuedTime = m.EnqueuedTime,
                MessageId = m.MessageId,
                Subject = m.Subject,
                CorrelationId = m.CorrelationId
            })
            .OrderBy(m => m.SequenceNumber)
            .ToList();
    }

    public async Task<IReadOnlyList<MessagePreview>> PeekQueueDeadLetterAsync(string connectionString, string queueName, int maxMessages = 50, long? fromSequenceNumber = null, CancellationToken ct = default)
    {
        await using var client = new ServiceBusClient(connectionString);
        await using var receiver = client.CreateReceiver(queueName, new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter });

        var messages = await receiver.PeekMessagesAsync(maxMessages, fromSequenceNumber, ct).ConfigureAwait(false);

        return messages
            .Select(m => new MessagePreview
            {
                SequenceNumber = m.SequenceNumber,
                EnqueuedTime = m.EnqueuedTime,
                MessageId = m.MessageId,
                Subject = m.Subject,
                CorrelationId = m.CorrelationId
            })
            .OrderBy(m => m.SequenceNumber)
            .ToList();
    }

    public async Task<IReadOnlyList<MessagePreview>> PeekSubscriptionAsync(string connectionString, string topicName, string subscriptionName, int maxMessages = 50, long? fromSequenceNumber = null, CancellationToken ct = default)
    {
        await using var client = new ServiceBusClient(connectionString);
        await using var receiver = client.CreateReceiver(topicName, subscriptionName);

        var messages = await receiver.PeekMessagesAsync(maxMessages, fromSequenceNumber, ct).ConfigureAwait(false);

        return messages
            .Select(m => new MessagePreview
            {
                SequenceNumber = m.SequenceNumber,
                EnqueuedTime = m.EnqueuedTime,
                MessageId = m.MessageId,
                Subject = m.Subject,
                CorrelationId = m.CorrelationId
            })
            .OrderBy(m => m.SequenceNumber)
            .ToList();
    }

    public async Task<IReadOnlyList<MessagePreview>> PeekSubscriptionDeadLetterAsync(string connectionString, string topicName, string subscriptionName, int maxMessages = 50, long? fromSequenceNumber = null, CancellationToken ct = default)
    {
        await using var client = new ServiceBusClient(connectionString);
        await using var receiver = client.CreateReceiver(topicName, subscriptionName, new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter });

        var messages = await receiver.PeekMessagesAsync(maxMessages, fromSequenceNumber, ct).ConfigureAwait(false);

        return messages
            .Select(m => new MessagePreview
            {
                SequenceNumber = m.SequenceNumber,
                EnqueuedTime = m.EnqueuedTime,
                MessageId = m.MessageId,
                Subject = m.Subject,
                CorrelationId = m.CorrelationId
            })
            .OrderBy(m => m.SequenceNumber)
            .ToList();
    }

    public async Task SendToQueueAsync(string connectionString, string queueName, string body, string? subject = null, string? correlationId = null, IDictionary<string, string>? properties = null, string? contentType = null, string? messageId = null, DateTimeOffset? scheduledEnqueueTime = null, CancellationToken ct = default)
    {
        await using var client = new ServiceBusClient(connectionString);
        var sender = client.CreateSender(queueName);
        var message = new ServiceBusMessage(BinaryData.FromString(body))
        {
            Subject = subject,
            CorrelationId = correlationId,
            ContentType = contentType
        };
        if (!string.IsNullOrEmpty(messageId))
        {
            message.MessageId = messageId;
        }
        if (properties is not null)
        {
            foreach (var kvp in properties)
                message.ApplicationProperties[kvp.Key] = kvp.Value;
        }
        if (scheduledEnqueueTime.HasValue && scheduledEnqueueTime.Value > DateTimeOffset.UtcNow)
        {
            await sender.ScheduleMessageAsync(message, scheduledEnqueueTime.Value, ct).ConfigureAwait(false);
        }
        else
        {
            await sender.SendMessageAsync(message, ct).ConfigureAwait(false);
        }
        await sender.DisposeAsync();
    }

    public async Task SendToTopicAsync(string connectionString, string topicName, string body, string? subject = null, string? correlationId = null, IDictionary<string, string>? properties = null, string? contentType = null, string? messageId = null, DateTimeOffset? scheduledEnqueueTime = null, CancellationToken ct = default)
    {
        await using var client = new ServiceBusClient(connectionString);
        var sender = client.CreateSender(topicName);
        var message = new ServiceBusMessage(BinaryData.FromString(body))
        {
            Subject = subject,
            CorrelationId = correlationId,
            ContentType = contentType
        };
        if (!string.IsNullOrEmpty(messageId))
        {
            message.MessageId = messageId;
        }
        if (properties is not null)
        {
            foreach (var kvp in properties)
                message.ApplicationProperties[kvp.Key] = kvp.Value;
        }
        if (scheduledEnqueueTime.HasValue && scheduledEnqueueTime.Value > DateTimeOffset.UtcNow)
        {
            await sender.ScheduleMessageAsync(message, scheduledEnqueueTime.Value, ct).ConfigureAwait(false);
        }
        else
        {
            await sender.SendMessageAsync(message, ct).ConfigureAwait(false);
        }
        await sender.DisposeAsync();
    }

    public async Task<int> PurgeQueueAsync(string connectionString, string queueName, int maxMessages = 1000, CancellationToken ct = default)
    {
        await using var client = new ServiceBusClient(connectionString);
        await using var receiver = client.CreateReceiver(queueName, new ServiceBusReceiverOptions { ReceiveMode = ServiceBusReceiveMode.ReceiveAndDelete });

        var deleted = 0;
        while (deleted < maxMessages && !ct.IsCancellationRequested)
        {
            var batchSize = Math.Min(200, maxMessages - deleted);
            var batch = await receiver.ReceiveMessagesAsync(batchSize, TimeSpan.FromSeconds(1), ct).ConfigureAwait(false);
            if (batch.Count == 0) break;
            deleted += batch.Count;
        }
        return deleted;
    }

    public async Task<int> PurgeQueueDeadLetterAsync(string connectionString, string queueName, int maxMessages = 1000, CancellationToken ct = default)
    {
        await using var client = new ServiceBusClient(connectionString);
        await using var receiver = client.CreateReceiver(queueName, new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter, ReceiveMode = ServiceBusReceiveMode.ReceiveAndDelete });

        var deleted = 0;
        while (deleted < maxMessages && !ct.IsCancellationRequested)
        {
            var batchSize = Math.Min(200, maxMessages - deleted);
            var batch = await receiver.ReceiveMessagesAsync(batchSize, TimeSpan.FromSeconds(1), ct).ConfigureAwait(false);
            if (batch.Count == 0) break;
            deleted += batch.Count;
        }
        return deleted;
    }

    public async Task<int> ReplayQueueDeadLettersAsync(string connectionString, string queueName, int maxMessages = 50, CancellationToken ct = default)
    {
        await using var client = new ServiceBusClient(connectionString);
        await using var dlqReceiver = client.CreateReceiver(queueName, new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter });
        var sender = client.CreateSender(queueName);

        var replayed = 0;
        while (replayed < maxMessages && !ct.IsCancellationRequested)
        {
            var batchSize = Math.Min(50, maxMessages - replayed);
            var messages = await dlqReceiver.ReceiveMessagesAsync(batchSize, TimeSpan.FromSeconds(2), ct).ConfigureAwait(false);
            if (messages.Count == 0) break;

            foreach (var m in messages)
            {
                var newMsg = new ServiceBusMessage(m.Body)
                {
                    Subject = m.Subject,
                    CorrelationId = m.CorrelationId,
                    MessageId = m.MessageId,
                    ContentType = m.ContentType
                };
                foreach (var kvp in m.ApplicationProperties)
                {
                    newMsg.ApplicationProperties[kvp.Key] = kvp.Value;
                }

                await sender.SendMessageAsync(newMsg, ct).ConfigureAwait(false);
                await dlqReceiver.CompleteMessageAsync(m, ct).ConfigureAwait(false);
                replayed++;
                if (replayed >= maxMessages) break;
            }
        }

        await sender.DisposeAsync();
        return replayed;
    }

    public async Task<MessageDetails?> PeekQueueMessageAsync(string connectionString, string queueName, long sequenceNumber, CancellationToken ct = default)
    {
        await using var client = new ServiceBusClient(connectionString);
        await using var receiver = client.CreateReceiver(queueName);
        var msg = await PeekOneBySequenceAsync(receiver, sequenceNumber, ct).ConfigureAwait(false);
        return msg is null ? null : MapDetails(msg);
    }

    public async Task<MessageDetails?> PeekQueueDeadLetterMessageAsync(string connectionString, string queueName, long sequenceNumber, CancellationToken ct = default)
    {
        await using var client = new ServiceBusClient(connectionString);
        await using var receiver = client.CreateReceiver(queueName, new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter });
        var msg = await PeekOneBySequenceAsync(receiver, sequenceNumber, ct).ConfigureAwait(false);
        return msg is null ? null : MapDetails(msg);
    }

    public async Task<MessageDetails?> PeekSubscriptionMessageAsync(string connectionString, string topicName, string subscriptionName, long sequenceNumber, CancellationToken ct = default)
    {
        await using var client = new ServiceBusClient(connectionString);
        await using var receiver = client.CreateReceiver(topicName, subscriptionName);
        var msg = await PeekOneBySequenceAsync(receiver, sequenceNumber, ct).ConfigureAwait(false);
        return msg is null ? null : MapDetails(msg);
    }

    public async Task<MessageDetails?> PeekSubscriptionDeadLetterMessageAsync(string connectionString, string topicName, string subscriptionName, long sequenceNumber, CancellationToken ct = default)
    {
        await using var client = new ServiceBusClient(connectionString);
        await using var receiver = client.CreateReceiver(topicName, subscriptionName, new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter });
        var msg = await PeekOneBySequenceAsync(receiver, sequenceNumber, ct).ConfigureAwait(false);
        return msg is null ? null : MapDetails(msg);
    }

    public async Task<int> PurgeSubscriptionAsync(string connectionString, string topicName, string subscriptionName, int maxMessages = 1000, CancellationToken ct = default)
    {
        await using var client = new ServiceBusClient(connectionString);
        await using var receiver = client.CreateReceiver(topicName, subscriptionName, new ServiceBusReceiverOptions { ReceiveMode = ServiceBusReceiveMode.ReceiveAndDelete });

        var deleted = 0;
        while (deleted < maxMessages && !ct.IsCancellationRequested)
        {
            var batchSize = Math.Min(200, maxMessages - deleted);
            var batch = await receiver.ReceiveMessagesAsync(batchSize, TimeSpan.FromSeconds(1), ct).ConfigureAwait(false);
            if (batch.Count == 0) break;
            deleted += batch.Count;
        }
        return deleted;
    }

    public async Task<int> PurgeSubscriptionDeadLetterAsync(string connectionString, string topicName, string subscriptionName, int maxMessages = 1000, CancellationToken ct = default)
    {
        await using var client = new ServiceBusClient(connectionString);
        await using var receiver = client.CreateReceiver(topicName, subscriptionName, new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter, ReceiveMode = ServiceBusReceiveMode.ReceiveAndDelete });

        var deleted = 0;
        while (deleted < maxMessages && !ct.IsCancellationRequested)
        {
            var batchSize = Math.Min(200, maxMessages - deleted);
            var batch = await receiver.ReceiveMessagesAsync(batchSize, TimeSpan.FromSeconds(1), ct).ConfigureAwait(false);
            if (batch.Count == 0) break;
            deleted += batch.Count;
        }
        return deleted;
    }

    private static async Task<ServiceBusReceivedMessage?> PeekOneBySequenceAsync(ServiceBusReceiver receiver, long sequenceNumber, CancellationToken ct)
    {
        var one = await receiver.PeekMessagesAsync(1, sequenceNumber, ct).ConfigureAwait(false);
        var candidate = one.FirstOrDefault();
        if (candidate != null && candidate.SequenceNumber == sequenceNumber)
            return candidate;

        if (sequenceNumber > 0)
        {
            var two = await receiver.PeekMessagesAsync(2, sequenceNumber - 1, ct).ConfigureAwait(false);
            var exact = two.FirstOrDefault(m => m.SequenceNumber == sequenceNumber);
            if (exact != null) return exact;
        }
        return null;
    }

    private static MessageDetails MapDetails(ServiceBusReceivedMessage m)
    {
        var appProps = new Dictionary<string, object?>();
        foreach (var kv in m.ApplicationProperties)
            appProps[kv.Key] = kv.Value;

        return new MessageDetails
        {
            SequenceNumber = m.SequenceNumber,
            EnqueuedTime = m.EnqueuedTime,
            ExpiresAt = m.ExpiresAt,
            ScheduledEnqueueTime = m.ScheduledEnqueueTime,
            DeliveryCount = m.DeliveryCount,
            MessageId = m.MessageId,
            Subject = m.Subject,
            CorrelationId = m.CorrelationId,
            ContentType = m.ContentType,
            To = m.To,
            ReplyTo = m.ReplyTo,
            SessionId = m.SessionId,
            PartitionKey = m.PartitionKey,
            ReplyToSessionId = m.ReplyToSessionId,
            TimeToLive = m.TimeToLive,
            Body = m.Body.ToString(),
            ApplicationProperties = appProps,
            DeadLetterSource = m.DeadLetterSource,
            DeadLetterReason = m.DeadLetterReason,
            DeadLetterErrorDescription = m.DeadLetterErrorDescription
        };
    }

    public async Task<bool> RemoveQueueDeadLetterMessageAsync(string connectionString, string queueName, long sequenceNumber, CancellationToken ct = default)
    {
        await using var client = new ServiceBusClient(connectionString);
        await using var receiver = client.CreateReceiver(queueName, new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter, ReceiveMode = ServiceBusReceiveMode.PeekLock });
        return await RemoveFromReceiverBySequenceAsync(receiver, sequenceNumber, ct).ConfigureAwait(false);
    }

    public async Task<bool> RemoveSubscriptionDeadLetterMessageAsync(string connectionString, string topicName, string subscriptionName, long sequenceNumber, CancellationToken ct = default)
    {
        await using var client = new ServiceBusClient(connectionString);
        await using var receiver = client.CreateReceiver(topicName, subscriptionName, new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter, ReceiveMode = ServiceBusReceiveMode.PeekLock });
        return await RemoveFromReceiverBySequenceAsync(receiver, sequenceNumber, ct).ConfigureAwait(false);
    }

    private static async Task<bool> RemoveFromReceiverBySequenceAsync(ServiceBusReceiver receiver, long sequenceNumber, CancellationToken ct)
    {
        const int maxScan = 200;
        var scanned = 0;
        while (scanned < maxScan && !ct.IsCancellationRequested)
        {
            var batchSize = Math.Min(20, maxScan - scanned);
            var messages = await receiver.ReceiveMessagesAsync(batchSize, TimeSpan.FromSeconds(2), ct).ConfigureAwait(false);
            if (messages.Count == 0)
                break;

            foreach (var msg in messages)
            {
                if (msg.SequenceNumber == sequenceNumber)
                {
                    await receiver.CompleteMessageAsync(msg, ct).ConfigureAwait(false);
                    foreach (var other in messages)
                    {
                        if (other != msg)
                        {
                            try { await receiver.AbandonMessageAsync(other, cancellationToken: ct).ConfigureAwait(false); } catch { }
                        }
                    }
                    return true;
                }
            }

            foreach (var m in messages)
            {
                try { await receiver.AbandonMessageAsync(m, cancellationToken: ct).ConfigureAwait(false); } catch { }
            }
            scanned += messages.Count;
        }
        return false;
    }
}
