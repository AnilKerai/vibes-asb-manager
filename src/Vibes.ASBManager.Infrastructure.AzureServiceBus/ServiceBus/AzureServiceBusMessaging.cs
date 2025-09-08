using System.Diagnostics.CodeAnalysis;
using Azure.Messaging.ServiceBus;
using Vibes.ASBManager.Application.Interfaces;
using Vibes.ASBManager.Application.Interfaces.Messaging;
using Vibes.ASBManager.Application.Models;

namespace Vibes.ASBManager.Infrastructure.AzureServiceBus.ServiceBus;

[ExcludeFromCodeCoverage]
public sealed class AzureServiceBusMessaging : IMessageBrowser, IMessageSender, IMessageMaintenance, IDeadLetterMaintenance
{
    public async Task<IReadOnlyList<MessagePreview>> PeekQueueAsync(string connectionString, string queueName, int maxMessages = 50, long? fromSequenceNumber = null, CancellationToken cancellationToken = default)
    {
        await using var client = new ServiceBusClient(connectionString);
        await using var receiver = client.CreateReceiver(queueName);

        var messages = await receiver.PeekMessagesAsync(maxMessages, fromSequenceNumber, cancellationToken).ConfigureAwait(false);

        return messages
            .Select(receivedMessage => new MessagePreview
            {
                SequenceNumber = receivedMessage.SequenceNumber,
                EnqueuedTime = receivedMessage.EnqueuedTime,
                MessageId = receivedMessage.MessageId,
                Subject = receivedMessage.Subject,
                CorrelationId = receivedMessage.CorrelationId
            })
            .OrderBy(preview => preview.SequenceNumber)
            .ToList();
    }

    public async Task<IReadOnlyList<MessagePreview>> PeekQueueDeadLetterAsync(string connectionString, string queueName, int maxMessages = 50, long? fromSequenceNumber = null, CancellationToken cancellationToken = default)
    {
        await using var client = new ServiceBusClient(connectionString);
        await using var receiver = client.CreateReceiver(queueName, new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter });

        var messages = await receiver.PeekMessagesAsync(maxMessages, fromSequenceNumber, cancellationToken).ConfigureAwait(false);

        return messages
            .Select(receivedMessage => new MessagePreview
            {
                SequenceNumber = receivedMessage.SequenceNumber,
                EnqueuedTime = receivedMessage.EnqueuedTime,
                MessageId = receivedMessage.MessageId,
                Subject = receivedMessage.Subject,
                CorrelationId = receivedMessage.CorrelationId
            })
            .OrderBy(preview => preview.SequenceNumber)
            .ToList();
    }

    public async Task<IReadOnlyList<MessagePreview>> PeekSubscriptionAsync(string connectionString, string topicName, string subscriptionName, int maxMessages = 50, long? fromSequenceNumber = null, CancellationToken cancellationToken = default)
    {
        await using var client = new ServiceBusClient(connectionString);
        await using var receiver = client.CreateReceiver(topicName, subscriptionName);

        var messages = await receiver.PeekMessagesAsync(maxMessages, fromSequenceNumber, cancellationToken).ConfigureAwait(false);

        return messages
            .Select(receivedMessage => new MessagePreview
            {
                SequenceNumber = receivedMessage.SequenceNumber,
                EnqueuedTime = receivedMessage.EnqueuedTime,
                MessageId = receivedMessage.MessageId,
                Subject = receivedMessage.Subject,
                CorrelationId = receivedMessage.CorrelationId
            })
            .OrderBy(preview => preview.SequenceNumber)
            .ToList();
    }

    public async Task<IReadOnlyList<MessagePreview>> PeekSubscriptionDeadLetterAsync(string connectionString, string topicName, string subscriptionName, int maxMessages = 50, long? fromSequenceNumber = null, CancellationToken cancellationToken = default)
    {
        await using var client = new ServiceBusClient(connectionString);
        await using var receiver = client.CreateReceiver(topicName, subscriptionName, new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter });

        var messages = await receiver.PeekMessagesAsync(maxMessages, fromSequenceNumber, cancellationToken).ConfigureAwait(false);

        return messages
            .Select(receivedMessage => new MessagePreview
            {
                SequenceNumber = receivedMessage.SequenceNumber,
                EnqueuedTime = receivedMessage.EnqueuedTime,
                MessageId = receivedMessage.MessageId,
                Subject = receivedMessage.Subject,
                CorrelationId = receivedMessage.CorrelationId
            })
            .OrderBy(preview => preview.SequenceNumber)
            .ToList();
    }

    public async Task SendToQueueAsync(string connectionString, string queueName, string body, string? subject = null, string? correlationId = null, IDictionary<string, string>? properties = null, string? contentType = null, string? messageId = null, DateTimeOffset? scheduledEnqueueTime = null, CancellationToken cancellationToken = default)
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
            foreach (var applicationProperty in properties)
                message.ApplicationProperties[applicationProperty.Key] = applicationProperty.Value;
        }
        if (scheduledEnqueueTime.HasValue && scheduledEnqueueTime.Value > DateTimeOffset.UtcNow)
        {
            await sender.ScheduleMessageAsync(message, scheduledEnqueueTime.Value, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await sender.SendMessageAsync(message, cancellationToken).ConfigureAwait(false);
        }
        await sender.DisposeAsync();
    }

    public async Task SendToTopicAsync(string connectionString, string topicName, string body, string? subject = null, string? correlationId = null, IDictionary<string, string>? properties = null, string? contentType = null, string? messageId = null, DateTimeOffset? scheduledEnqueueTime = null, CancellationToken cancellationToken = default)
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
            foreach (var applicationProperty in properties)
                message.ApplicationProperties[applicationProperty.Key] = applicationProperty.Value;
        }
        if (scheduledEnqueueTime.HasValue && scheduledEnqueueTime.Value > DateTimeOffset.UtcNow)
        {
            await sender.ScheduleMessageAsync(message, scheduledEnqueueTime.Value, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await sender.SendMessageAsync(message, cancellationToken).ConfigureAwait(false);
        }
        await sender.DisposeAsync();
    }

    public async Task<int> PurgeQueueAsync(string connectionString, string queueName, int maxMessages = 1000, CancellationToken cancellationToken = default)
    {
        await using var client = new ServiceBusClient(connectionString);
        await using var receiver = client.CreateReceiver(queueName, new ServiceBusReceiverOptions { ReceiveMode = ServiceBusReceiveMode.ReceiveAndDelete });

        var deletedCount = 0;
        while (deletedCount < maxMessages && !cancellationToken.IsCancellationRequested)
        {
            var batchSize = Math.Min(200, maxMessages - deletedCount);
            var receivedBatch = await receiver.ReceiveMessagesAsync(batchSize, TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
            if (receivedBatch.Count == 0) break;
            deletedCount += receivedBatch.Count;
        }
        return deletedCount;
    }

    public async Task<int> PurgeQueueDeadLetterAsync(string connectionString, string queueName, int maxMessages = 1000, CancellationToken cancellationToken = default)
    {
        await using var client = new ServiceBusClient(connectionString);
        await using var receiver = client.CreateReceiver(queueName, new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter, ReceiveMode = ServiceBusReceiveMode.ReceiveAndDelete });

        var deletedCount = 0;
        while (deletedCount < maxMessages && !cancellationToken.IsCancellationRequested)
        {
            var batchSize = Math.Min(200, maxMessages - deletedCount);
            var receivedBatch = await receiver.ReceiveMessagesAsync(batchSize, TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
            if (receivedBatch.Count == 0) break;
            deletedCount += receivedBatch.Count;
        }
        return deletedCount;
    }

    public async Task<int> ReplayQueueDeadLettersAsync(string connectionString, string queueName, int maxMessages = 50, CancellationToken cancellationToken = default)
    {
        await using var client = new ServiceBusClient(connectionString);
        await using var deadLetterReceiver = client.CreateReceiver(queueName, new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter });
        var sender = client.CreateSender(queueName);

        var replayedCount = 0;
        while (replayedCount < maxMessages && !cancellationToken.IsCancellationRequested)
        {
            var batchSize = Math.Min(50, maxMessages - replayedCount);
            var deadLetterMessages = await deadLetterReceiver.ReceiveMessagesAsync(batchSize, TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);
            if (deadLetterMessages.Count == 0) break;

            foreach (var deadLetterMessage in deadLetterMessages)
            {
                var replayMessage = new ServiceBusMessage(deadLetterMessage.Body)
                {
                    Subject = deadLetterMessage.Subject,
                    CorrelationId = deadLetterMessage.CorrelationId,
                    MessageId = deadLetterMessage.MessageId,
                    ContentType = deadLetterMessage.ContentType
                };
                foreach (var property in deadLetterMessage.ApplicationProperties)
                {
                    replayMessage.ApplicationProperties[property.Key] = property.Value;
                }

                await sender.SendMessageAsync(replayMessage, cancellationToken).ConfigureAwait(false);
                await deadLetterReceiver.CompleteMessageAsync(deadLetterMessage, cancellationToken).ConfigureAwait(false);
                replayedCount++;
                if (replayedCount >= maxMessages) break;
            }
        }

        await sender.DisposeAsync();
        return replayedCount;
    }

    public async Task<MessageDetails?> PeekQueueMessageAsync(string connectionString, string queueName, long sequenceNumber, CancellationToken cancellationToken = default)
    {
        await using var client = new ServiceBusClient(connectionString);
        await using var receiver = client.CreateReceiver(queueName);
        var receivedMessage = await PeekOneBySequenceAsync(receiver, sequenceNumber, cancellationToken).ConfigureAwait(false);
        return receivedMessage is null ? null : MapDetails(receivedMessage);
    }

    public async Task<MessageDetails?> PeekQueueDeadLetterMessageAsync(string connectionString, string queueName, long sequenceNumber, CancellationToken cancellationToken = default)
    {
        await using var client = new ServiceBusClient(connectionString);
        await using var receiver = client.CreateReceiver(queueName, new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter });
        var receivedMessage = await PeekOneBySequenceAsync(receiver, sequenceNumber, cancellationToken).ConfigureAwait(false);
        return receivedMessage is null ? null : MapDetails(receivedMessage);
    }

    public async Task<MessageDetails?> PeekSubscriptionMessageAsync(string connectionString, string topicName, string subscriptionName, long sequenceNumber, CancellationToken cancellationToken = default)
    {
        await using var client = new ServiceBusClient(connectionString);
        await using var receiver = client.CreateReceiver(topicName, subscriptionName);
        var receivedMessage = await PeekOneBySequenceAsync(receiver, sequenceNumber, cancellationToken).ConfigureAwait(false);
        return receivedMessage is null ? null : MapDetails(receivedMessage);
    }

    public async Task<MessageDetails?> PeekSubscriptionDeadLetterMessageAsync(string connectionString, string topicName, string subscriptionName, long sequenceNumber, CancellationToken cancellationToken = default)
    {
        await using var client = new ServiceBusClient(connectionString);
        await using var receiver = client.CreateReceiver(topicName, subscriptionName, new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter });
        var receivedMessage = await PeekOneBySequenceAsync(receiver, sequenceNumber, cancellationToken).ConfigureAwait(false);
        return receivedMessage is null ? null : MapDetails(receivedMessage);
    }

    public async Task<int> PurgeSubscriptionAsync(string connectionString, string topicName, string subscriptionName, int maxMessages = 1000, CancellationToken cancellationToken = default)
    {
        await using var client = new ServiceBusClient(connectionString);
        await using var receiver = client.CreateReceiver(topicName, subscriptionName, new ServiceBusReceiverOptions { ReceiveMode = ServiceBusReceiveMode.ReceiveAndDelete });

        var deletedCount = 0;
        while (deletedCount < maxMessages && !cancellationToken.IsCancellationRequested)
        {
            var batchSize = Math.Min(200, maxMessages - deletedCount);
            var receivedBatch = await receiver.ReceiveMessagesAsync(batchSize, TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
            if (receivedBatch.Count == 0) break;
            deletedCount += receivedBatch.Count;
        }
        return deletedCount;
    }

    public async Task<int> PurgeSubscriptionDeadLetterAsync(string connectionString, string topicName, string subscriptionName, int maxMessages = 1000, CancellationToken cancellationToken = default)
    {
        await using var client = new ServiceBusClient(connectionString);
        await using var receiver = client.CreateReceiver(topicName, subscriptionName, new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter, ReceiveMode = ServiceBusReceiveMode.ReceiveAndDelete });

        var deletedCount = 0;
        while (deletedCount < maxMessages && !cancellationToken.IsCancellationRequested)
        {
            var batchSize = Math.Min(200, maxMessages - deletedCount);
            var receivedBatch = await receiver.ReceiveMessagesAsync(batchSize, TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
            if (receivedBatch.Count == 0) break;
            deletedCount += receivedBatch.Count;
        }
        return deletedCount;
    }

    private static async Task<ServiceBusReceivedMessage?> PeekOneBySequenceAsync(ServiceBusReceiver receiver, long sequenceNumber, CancellationToken cancellationToken)
    {
        var firstPeek = await receiver.PeekMessagesAsync(1, sequenceNumber, cancellationToken).ConfigureAwait(false);
        var candidateMessage = firstPeek.FirstOrDefault();
        if (candidateMessage != null && candidateMessage.SequenceNumber == sequenceNumber)
            return candidateMessage;

        if (sequenceNumber > 0)
        {
            var secondPeek = await receiver.PeekMessagesAsync(2, sequenceNumber - 1, cancellationToken).ConfigureAwait(false);
            var exactMatch = secondPeek.FirstOrDefault(message => message.SequenceNumber == sequenceNumber);
            if (exactMatch != null) return exactMatch;
        }
        return null;
    }

    private static MessageDetails MapDetails(ServiceBusReceivedMessage receivedMessage)
    {
        var applicationProperties = new Dictionary<string, object?>();
        foreach (var property in receivedMessage.ApplicationProperties)
            applicationProperties[property.Key] = property.Value;

        return new MessageDetails
        {
            SequenceNumber = receivedMessage.SequenceNumber,
            EnqueuedTime = receivedMessage.EnqueuedTime,
            ExpiresAt = receivedMessage.ExpiresAt,
            ScheduledEnqueueTime = receivedMessage.ScheduledEnqueueTime,
            DeliveryCount = receivedMessage.DeliveryCount,
            MessageId = receivedMessage.MessageId,
            Subject = receivedMessage.Subject,
            CorrelationId = receivedMessage.CorrelationId,
            ContentType = receivedMessage.ContentType,
            To = receivedMessage.To,
            ReplyTo = receivedMessage.ReplyTo,
            SessionId = receivedMessage.SessionId,
            PartitionKey = receivedMessage.PartitionKey,
            ReplyToSessionId = receivedMessage.ReplyToSessionId,
            TimeToLive = receivedMessage.TimeToLive,
            Body = receivedMessage.Body.ToString(),
            ApplicationProperties = applicationProperties,
            DeadLetterSource = receivedMessage.DeadLetterSource,
            DeadLetterReason = receivedMessage.DeadLetterReason,
            DeadLetterErrorDescription = receivedMessage.DeadLetterErrorDescription
        };
    }

    public async Task<bool> RemoveQueueDeadLetterMessageAsync(string connectionString, string queueName, long sequenceNumber, CancellationToken cancellationToken = default)
    {
        await using var client = new ServiceBusClient(connectionString);
        await using var receiver = client.CreateReceiver(queueName, new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter, ReceiveMode = ServiceBusReceiveMode.PeekLock });
        return await RemoveFromReceiverBySequenceAsync(receiver, sequenceNumber, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> RemoveSubscriptionDeadLetterMessageAsync(string connectionString, string topicName, string subscriptionName, long sequenceNumber, CancellationToken cancellationToken = default)
    {
        await using var client = new ServiceBusClient(connectionString);
        await using var receiver = client.CreateReceiver(topicName, subscriptionName, new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter, ReceiveMode = ServiceBusReceiveMode.PeekLock });
        return await RemoveFromReceiverBySequenceAsync(receiver, sequenceNumber, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<bool> RemoveFromReceiverBySequenceAsync(ServiceBusReceiver receiver, long sequenceNumber, CancellationToken cancellationToken)
    {
        const int maxMessagesToScan = 200;
        var scannedCount = 0;
        while (scannedCount < maxMessagesToScan && !cancellationToken.IsCancellationRequested)
        {
            var batchSize = Math.Min(20, maxMessagesToScan - scannedCount);
            var receivedMessages = await receiver.ReceiveMessagesAsync(batchSize, TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);
            if (receivedMessages.Count == 0)
                break;

            foreach (var message in receivedMessages)
            {
                if (message.SequenceNumber == sequenceNumber)
                {
                    await receiver.CompleteMessageAsync(message, cancellationToken).ConfigureAwait(false);
                    foreach (var otherMessage in receivedMessages)
                    {
                        if (otherMessage != message)
                        {
                            try { await receiver.AbandonMessageAsync(otherMessage, cancellationToken: cancellationToken).ConfigureAwait(false); } catch { }
                        }
                    }
                    return true;
                }
            }

            foreach (var messageToAbandon in receivedMessages)
            {
                try { await receiver.AbandonMessageAsync(messageToAbandon, cancellationToken: cancellationToken).ConfigureAwait(false); } catch { }
            }
            scannedCount += receivedMessages.Count;
        }
        return false;
    }
}
