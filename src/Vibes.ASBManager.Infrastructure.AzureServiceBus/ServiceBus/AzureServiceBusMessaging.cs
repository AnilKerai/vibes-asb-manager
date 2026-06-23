using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using Vibes.ASBManager.Application.Interfaces.Messaging;
using Vibes.ASBManager.Application.Messaging;
using Vibes.ASBManager.Application.Models;

namespace Vibes.ASBManager.Infrastructure.AzureServiceBus.ServiceBus;

[ExcludeFromCodeCoverage]
public sealed class AzureServiceBusMessaging(
    ILogger<AzureServiceBusMessaging> logger
) : IMessageBrowser, IMessageSender, IMessageMaintenance, IDeadLetterMaintenance, IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, ServiceBusClient> _clients = new(StringComparer.Ordinal);
    private readonly ILogger<AzureServiceBusMessaging> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    // internal (not private) so tests can assert the per-connection-string client cache that
    // keeps multiple open connections/tabs isolated.
    internal ServiceBusClient GetClient(string connectionString)
        => _clients.GetOrAdd(connectionString, static cs => new ServiceBusClient(cs));

    // Draining a session-enabled entity ends only when AcceptNextSession finds no more available
    // sessions, which the broker signals by letting the call time out. The shared cached client uses
    // the SDK defaults (60s TryTimeout x 3 retries), so that terminal "no more sessions" would stall a
    // purge for minutes after the last message is already gone. A dedicated short-timeout client bounds
    // that tail to a few seconds without lowering timeouts on the cached client that normal
    // browsing/sending (and the other open connections/tabs) rely on. 5s is ample to acquire any
    // available session; one retry guards against a transient miss without dragging the tail back out.
    private static readonly ServiceBusClientOptions SessionDrainClientOptions = new()
    {
        RetryOptions = new ServiceBusRetryOptions
        {
            TryTimeout = TimeSpan.FromSeconds(5),
            MaxRetries = 1,
        },
    };

    // Created per purge — a rare, explicit, destructive action — and disposed by the caller.
    private static ServiceBusClient CreateSessionDrainClient(string connectionString)
        => new(connectionString, SessionDrainClientOptions);

    private static ServiceBusMessage BuildMessage(
        string body,
        string? subject,
        string? correlationId,
        IDictionary<string, string>? properties,
        string? contentType,
        string? messageId)
    {
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
        return message;
    }

    public async Task<IReadOnlyList<MessagePreview>> PeekQueueAsync(string connectionString, string queueName, int maxMessages = 50, long? fromSequenceNumber = null, CancellationToken cancellationToken = default)
    {
        var client = GetClient(connectionString);
        await using var receiver = client.CreateReceiver(queueName);

        var messages = await receiver.PeekMessagesAsync(maxMessages, fromSequenceNumber, cancellationToken).ConfigureAwait(false);

        return messages
            .Select(ToPreview)
            .OrderBy(preview => preview.SequenceNumber)
            .ToList();
    }

    public async Task<IReadOnlyList<MessagePreview>> PeekQueueDeadLetterAsync(string connectionString, string queueName, int maxMessages = 50, long? fromSequenceNumber = null, CancellationToken cancellationToken = default)
    {
        var client = GetClient(connectionString);
        await using var receiver = client.CreateReceiver(queueName, new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter });

        var messages = await receiver.PeekMessagesAsync(maxMessages, fromSequenceNumber, cancellationToken).ConfigureAwait(false);

        return messages
            .Select(ToPreview)
            .OrderBy(preview => preview.SequenceNumber)
            .ToList();
    }

    public async Task<IReadOnlyList<MessagePreview>> PeekSubscriptionAsync(string connectionString, string topicName, string subscriptionName, int maxMessages = 50, long? fromSequenceNumber = null, CancellationToken cancellationToken = default)
    {
        var client = GetClient(connectionString);
        await using var receiver = client.CreateReceiver(topicName, subscriptionName);

        var messages = await receiver.PeekMessagesAsync(maxMessages, fromSequenceNumber, cancellationToken).ConfigureAwait(false);

        return messages
            .Select(ToPreview)
            .OrderBy(preview => preview.SequenceNumber)
            .ToList();
    }

    public async Task<IReadOnlyList<MessagePreview>> PeekSubscriptionDeadLetterAsync(string connectionString, string topicName, string subscriptionName, int maxMessages = 50, long? fromSequenceNumber = null, CancellationToken cancellationToken = default)
    {
        var client = GetClient(connectionString);
        await using var receiver = client.CreateReceiver(topicName, subscriptionName, new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter });

        var messages = await receiver.PeekMessagesAsync(maxMessages, fromSequenceNumber, cancellationToken).ConfigureAwait(false);

        return messages
            .Select(ToPreview)
            .OrderBy(preview => preview.SequenceNumber)
            .ToList();
    }

    private static MessagePreview ToPreview(ServiceBusReceivedMessage message) => new()
    {
        SequenceNumber = message.SequenceNumber,
        EnqueuedTime = message.EnqueuedTime,
        MessageId = message.MessageId,
        Subject = message.Subject,
        CorrelationId = message.CorrelationId
    };

    // Pages a snapshot behind a SINGLE receiver: the shared pager advances the peek anchor and we
    // pass it straight to PeekMessagesAsync on the same receiver, so a 500-message refresh issues
    // one receiver instead of one per page. Short/empty-batch tolerance lives in the pager.
    private static async Task<IReadOnlyList<MessagePreview>> CollectSnapshotAsync(
        ServiceBusReceiver receiver, int target, int fetchSize, int maxEmptyPeeks, CancellationToken cancellationToken)
    {
        var collected = await MessageSnapshotPager.CollectAsync(
            async (anchor, max, token) =>
            {
                var page = await receiver.PeekMessagesAsync(max, anchor, token).ConfigureAwait(false);
                IReadOnlyList<MessagePreview> previews = page.Select(ToPreview).ToList();
                return previews;
            },
            target, fetchSize, maxEmptyPeeks, cancellationToken).ConfigureAwait(false);

        return collected
            .OrderBy(preview => preview.SequenceNumber)
            .ToList();
    }

    public async Task<IReadOnlyList<MessagePreview>> PeekQueueSnapshotAsync(string connectionString, string queueName, int target, int fetchSize = 50, int maxEmptyPeeks = 3, CancellationToken cancellationToken = default)
    {
        var client = GetClient(connectionString);
        await using var receiver = client.CreateReceiver(queueName);
        return await CollectSnapshotAsync(receiver, target, fetchSize, maxEmptyPeeks, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<MessagePreview>> PeekQueueDeadLetterSnapshotAsync(string connectionString, string queueName, int target, int fetchSize = 50, int maxEmptyPeeks = 3, CancellationToken cancellationToken = default)
    {
        var client = GetClient(connectionString);
        await using var receiver = client.CreateReceiver(queueName, new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter });
        return await CollectSnapshotAsync(receiver, target, fetchSize, maxEmptyPeeks, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<MessagePreview>> PeekSubscriptionSnapshotAsync(string connectionString, string topicName, string subscriptionName, int target, int fetchSize = 50, int maxEmptyPeeks = 3, CancellationToken cancellationToken = default)
    {
        var client = GetClient(connectionString);
        await using var receiver = client.CreateReceiver(topicName, subscriptionName);
        return await CollectSnapshotAsync(receiver, target, fetchSize, maxEmptyPeeks, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<MessagePreview>> PeekSubscriptionDeadLetterSnapshotAsync(string connectionString, string topicName, string subscriptionName, int target, int fetchSize = 50, int maxEmptyPeeks = 3, CancellationToken cancellationToken = default)
    {
        var client = GetClient(connectionString);
        await using var receiver = client.CreateReceiver(topicName, subscriptionName, new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter });
        return await CollectSnapshotAsync(receiver, target, fetchSize, maxEmptyPeeks, cancellationToken).ConfigureAwait(false);
    }

    public async Task SendToQueueAsync(string connectionString, string queueName, string body, string? subject = null, string? correlationId = null, IDictionary<string, string>? properties = null, string? contentType = null, string? messageId = null, DateTimeOffset? scheduledEnqueueTime = null, CancellationToken cancellationToken = default)
    {
        var client = GetClient(connectionString);
        await using var sender = client.CreateSender(queueName);
        var message = BuildMessage(body, subject, correlationId, properties, contentType, messageId);
        if (scheduledEnqueueTime.HasValue && scheduledEnqueueTime.Value > DateTimeOffset.UtcNow)
        {
            await sender.ScheduleMessageAsync(message, scheduledEnqueueTime.Value, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await sender.SendMessageAsync(message, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task SendToTopicAsync(string connectionString, string topicName, string body, string? subject = null, string? correlationId = null, IDictionary<string, string>? properties = null, string? contentType = null, string? messageId = null, DateTimeOffset? scheduledEnqueueTime = null, CancellationToken cancellationToken = default)
    {
        var client = GetClient(connectionString);
        await using var sender = client.CreateSender(topicName);
        var message = BuildMessage(body, subject, correlationId, properties, contentType, messageId);
        if (scheduledEnqueueTime.HasValue && scheduledEnqueueTime.Value > DateTimeOffset.UtcNow)
        {
            await sender.ScheduleMessageAsync(message, scheduledEnqueueTime.Value, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await sender.SendMessageAsync(message, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task SendBatchToQueueAsync(string connectionString, string queueName, string body, int count, string? subject = null, string? correlationId = null, IDictionary<string, string>? properties = null, string? contentType = null, string? messageId = null, CancellationToken cancellationToken = default)
    {
        if (count < 1) return;
        var client = GetClient(connectionString);
        await using var sender = client.CreateSender(queueName);
        await SendBatchAsync(sender, body, count, subject, correlationId, properties, contentType, messageId, cancellationToken).ConfigureAwait(false);
    }

    public async Task SendBatchToTopicAsync(string connectionString, string topicName, string body, int count, string? subject = null, string? correlationId = null, IDictionary<string, string>? properties = null, string? contentType = null, string? messageId = null, CancellationToken cancellationToken = default)
    {
        if (count < 1) return;
        var client = GetClient(connectionString);
        await using var sender = client.CreateSender(topicName);
        await SendBatchAsync(sender, body, count, subject, correlationId, properties, contentType, messageId, cancellationToken).ConfigureAwait(false);
    }

    private static async Task SendBatchAsync(
        ServiceBusSender sender,
        string body,
        int count,
        string? subject,
        string? correlationId,
        IDictionary<string, string>? properties,
        string? contentType,
        string? messageId,
        CancellationToken cancellationToken)
    {
        var remaining = count;
        while (remaining > 0)
        {
            using var batch = await sender.CreateMessageBatchAsync(cancellationToken).ConfigureAwait(false);
            var added = 0;
            while (remaining > 0)
            {
                var message = BuildMessage(body, subject, correlationId, properties, contentType, messageId);
                if (!batch.TryAddMessage(message))
                {
                    if (added == 0)
                        throw new InvalidOperationException("Message is too large to fit in a batch.");
                    break;
                }
                added++;
                remaining--;
            }

            if (added > 0)
                await sender.SendMessagesAsync(batch, cancellationToken).ConfigureAwait(false);
        }
    }

    // Builds a message to resend a dead-letter message back to its source entity. SessionId is
    // preserved so session-enabled entities accept the resend (it's ignored by non-session entities).
    internal static ServiceBusMessage BuildReplayMessage(ServiceBusReceivedMessage source)
    {
        var replayMessage = new ServiceBusMessage(source.Body)
        {
            Subject = source.Subject,
            CorrelationId = source.CorrelationId,
            MessageId = source.MessageId,
            ContentType = source.ContentType,
            SessionId = source.SessionId
        };
        foreach (var property in source.ApplicationProperties)
            replayMessage.ApplicationProperties[property.Key] = property.Value;
        return replayMessage;
    }

    // Drains every available session of a session-enabled entity using ReceiveAndDelete. "No more
    // unlocked sessions" surfaces as a ServiceBusException with reason ServiceTimeout. Only the active
    // queue/subscription is session-scoped; the dead-letter sub-queue is read with a regular receiver.
    private delegate Task<ServiceBusSessionReceiver> AcceptNextSession(ServiceBusSessionReceiverOptions options, CancellationToken cancellationToken);

    private static async Task<int> PurgeSessionsAsync(AcceptNextSession acceptNextSession, int maxMessages, IProgress<int>? progress, CancellationToken cancellationToken)
    {
        var options = new ServiceBusSessionReceiverOptions { ReceiveMode = ServiceBusReceiveMode.ReceiveAndDelete };
        var deletedCount = 0;
        while (deletedCount < maxMessages && !cancellationToken.IsCancellationRequested)
        {
            ServiceBusSessionReceiver receiver;
            try
            {
                receiver = await acceptNextSession(options, cancellationToken).ConfigureAwait(false);
            }
            catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.ServiceTimeout)
            {
                break; // no more unlocked sessions with active messages
            }

            await using (receiver)
            {
                while (deletedCount < maxMessages && !cancellationToken.IsCancellationRequested)
                {
                    var batchSize = Math.Min(200, maxMessages - deletedCount);
                    var batch = await receiver.ReceiveMessagesAsync(batchSize, TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
                    if (batch.Count == 0) break;
                    deletedCount += batch.Count;
                    progress?.Report(deletedCount);
                }
            }
        }
        return deletedCount;
    }

    // A lone empty ReceiveAndDelete batch doesn't mean the entity is drained — broker pacing or a cold
    // link can return empty while messages remain — so retry a few consecutive empties before concluding
    // it's empty, mirroring the peek pager. (The session path doesn't need this: its outer
    // AcceptNextSession loop re-acquires any session that still holds messages.)
    private const int MaxEmptyDrainReceives = 3;

    // Drains an entity to empty (or the safety ceiling) through one receiver, reporting the running
    // total. The caller passes a factory rather than a receiver so the first receive still surfaces
    // InvalidOperationException for a session entity (letting the caller fall back to the session drain).
    private static async Task<int> DrainEntityAsync(Func<ServiceBusReceiver> receiverFactory, int maxMessages, IProgress<int>? progress, CancellationToken cancellationToken)
    {
        await using var receiver = receiverFactory();
        var deletedCount = 0;
        var emptyReceives = 0;
        while (deletedCount < maxMessages && !cancellationToken.IsCancellationRequested)
        {
            var batchSize = Math.Min(200, maxMessages - deletedCount);
            var batch = await receiver.ReceiveMessagesAsync(batchSize, TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
            if (batch.Count == 0)
            {
                if (++emptyReceives > MaxEmptyDrainReceives) break;
                continue;
            }
            emptyReceives = 0;
            deletedCount += batch.Count;
            progress?.Report(deletedCount);
        }
        return deletedCount;
    }

    public async Task<int> PurgeQueueAsync(string connectionString, string queueName, int maxMessages = MessagingDefaults.PurgeCeiling, IProgress<int>? progress = null, CancellationToken cancellationToken = default)
    {
        var client = GetClient(connectionString);
        try
        {
            return await DrainEntityAsync(
                () => client.CreateReceiver(queueName, new ServiceBusReceiverOptions { ReceiveMode = ServiceBusReceiveMode.ReceiveAndDelete }),
                maxMessages, progress, cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
            // Session-enabled queue: a non-session receiver can't receive from it. Drain each session
            // with a short-timeout client so the terminal "no more sessions" doesn't stall the purge.
            await using var sessionClient = CreateSessionDrainClient(connectionString);
            return await PurgeSessionsAsync((options, token) => sessionClient.AcceptNextSessionAsync(queueName, options, token), maxMessages, progress, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<int> PurgeQueueDeadLetterAsync(string connectionString, string queueName, int maxMessages = MessagingDefaults.PurgeCeiling, IProgress<int>? progress = null, CancellationToken cancellationToken = default)
    {
        var client = GetClient(connectionString);
        return await DrainEntityAsync(
            () => client.CreateReceiver(queueName, new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter, ReceiveMode = ServiceBusReceiveMode.ReceiveAndDelete }),
            maxMessages, progress, cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> ReplayQueueDeadLettersAsync(string connectionString, string queueName, int maxMessages = 50, CancellationToken cancellationToken = default)
    {
        var client = GetClient(connectionString);
        await using var deadLetterReceiver = client.CreateReceiver(queueName, new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter });
        await using var sender = client.CreateSender(queueName);

        var replayedCount = 0;
        while (replayedCount < maxMessages && !cancellationToken.IsCancellationRequested)
        {
            var batchSize = Math.Min(50, maxMessages - replayedCount);
            var deadLetterMessages = await deadLetterReceiver.ReceiveMessagesAsync(batchSize, TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);
            if (deadLetterMessages.Count == 0) break;

            foreach (var deadLetterMessage in deadLetterMessages)
            {
                await sender.SendMessageAsync(BuildReplayMessage(deadLetterMessage), cancellationToken).ConfigureAwait(false);
                await deadLetterReceiver.CompleteMessageAsync(deadLetterMessage, cancellationToken).ConfigureAwait(false);
                replayedCount++;
                if (replayedCount >= maxMessages) break;
            }
        }

        return replayedCount;
    }

    public async Task<int> ReplaySubscriptionDeadLettersAsync(string connectionString, string topicName, string subscriptionName, int maxMessages = 50, CancellationToken cancellationToken = default)
    {
        var client = GetClient(connectionString);
        await using var deadLetterReceiver = client.CreateReceiver(topicName, subscriptionName, new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter });
        await using var sender = client.CreateSender(topicName);

        var replayedCount = 0;
        while (replayedCount < maxMessages && !cancellationToken.IsCancellationRequested)
        {
            var batchSize = Math.Min(50, maxMessages - replayedCount);
            var deadLetterMessages = await deadLetterReceiver.ReceiveMessagesAsync(batchSize, TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);
            if (deadLetterMessages.Count == 0) break;

            foreach (var deadLetterMessage in deadLetterMessages)
            {
                await sender.SendMessageAsync(BuildReplayMessage(deadLetterMessage), cancellationToken).ConfigureAwait(false);
                await deadLetterReceiver.CompleteMessageAsync(deadLetterMessage, cancellationToken).ConfigureAwait(false);
                replayedCount++;
                if (replayedCount >= maxMessages) break;
            }
        }

        return replayedCount;
    }

    public async Task<MessageDetails?> PeekQueueMessageAsync(string connectionString, string queueName, long sequenceNumber, CancellationToken cancellationToken = default)
    {
        var client = GetClient(connectionString);
        await using var receiver = client.CreateReceiver(queueName);
        var receivedMessage = await PeekOneBySequenceAsync(receiver, sequenceNumber, cancellationToken).ConfigureAwait(false);
        return receivedMessage is null ? null : MapDetails(receivedMessage);
    }

    public async Task<MessageDetails?> PeekQueueDeadLetterMessageAsync(string connectionString, string queueName, long sequenceNumber, CancellationToken cancellationToken = default)
    {
        var client = GetClient(connectionString);
        await using var receiver = client.CreateReceiver(queueName, new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter });
        var receivedMessage = await PeekOneBySequenceAsync(receiver, sequenceNumber, cancellationToken).ConfigureAwait(false);
        return receivedMessage is null ? null : MapDetails(receivedMessage);
    }

    public async Task<MessageDetails?> PeekSubscriptionMessageAsync(string connectionString, string topicName, string subscriptionName, long sequenceNumber, CancellationToken cancellationToken = default)
    {
        var client = GetClient(connectionString);
        await using var receiver = client.CreateReceiver(topicName, subscriptionName);
        var receivedMessage = await PeekOneBySequenceAsync(receiver, sequenceNumber, cancellationToken).ConfigureAwait(false);
        return receivedMessage is null ? null : MapDetails(receivedMessage);
    }

    public async Task<MessageDetails?> PeekSubscriptionDeadLetterMessageAsync(string connectionString, string topicName, string subscriptionName, long sequenceNumber, CancellationToken cancellationToken = default)
    {
        var client = GetClient(connectionString);
        await using var receiver = client.CreateReceiver(topicName, subscriptionName, new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter });
        var receivedMessage = await PeekOneBySequenceAsync(receiver, sequenceNumber, cancellationToken).ConfigureAwait(false);
        return receivedMessage is null ? null : MapDetails(receivedMessage);
    }

    public async Task<int> PurgeSubscriptionAsync(string connectionString, string topicName, string subscriptionName, int maxMessages = MessagingDefaults.PurgeCeiling, IProgress<int>? progress = null, CancellationToken cancellationToken = default)
    {
        var client = GetClient(connectionString);
        try
        {
            return await DrainEntityAsync(
                () => client.CreateReceiver(topicName, subscriptionName, new ServiceBusReceiverOptions { ReceiveMode = ServiceBusReceiveMode.ReceiveAndDelete }),
                maxMessages, progress, cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
            // Session-enabled subscription: a non-session receiver can't receive from it. Drain each
            // session with a short-timeout client so the terminal "no more sessions" doesn't stall it.
            await using var sessionClient = CreateSessionDrainClient(connectionString);
            return await PurgeSessionsAsync((options, token) => sessionClient.AcceptNextSessionAsync(topicName, subscriptionName, options, token), maxMessages, progress, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<int> PurgeSubscriptionDeadLetterAsync(string connectionString, string topicName, string subscriptionName, int maxMessages = MessagingDefaults.PurgeCeiling, IProgress<int>? progress = null, CancellationToken cancellationToken = default)
    {
        var client = GetClient(connectionString);
        return await DrainEntityAsync(
            () => client.CreateReceiver(topicName, subscriptionName, new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter, ReceiveMode = ServiceBusReceiveMode.ReceiveAndDelete }),
            maxMessages, progress, cancellationToken).ConfigureAwait(false);
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
        var client = GetClient(connectionString);
        await using var receiver = client.CreateReceiver(queueName, new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter, ReceiveMode = ServiceBusReceiveMode.PeekLock });
        return await RemoveFromReceiverBySequenceAsync(receiver, sequenceNumber, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> RemoveSubscriptionDeadLetterMessageAsync(string connectionString, string topicName, string subscriptionName, long sequenceNumber, CancellationToken cancellationToken = default)
    {
        var client = GetClient(connectionString);
        await using var receiver = client.CreateReceiver(topicName, subscriptionName, new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter, ReceiveMode = ServiceBusReceiveMode.PeekLock });
        return await RemoveFromReceiverBySequenceAsync(receiver, sequenceNumber, cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> RemoveFromReceiverBySequenceAsync(ServiceBusReceiver receiver, long sequenceNumber, CancellationToken cancellationToken)
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
                            try
                            {
                                await receiver.AbandonMessageAsync(otherMessage, cancellationToken: cancellationToken).ConfigureAwait(false);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Failed to abandon dead-letter message {SequenceNumber}", otherMessage.SequenceNumber);
                            }
                        }
                    }
                    return true;
                }
            }

            foreach (var messageToAbandon in receivedMessages)
            {
                try
                {
                    await receiver.AbandonMessageAsync(messageToAbandon, cancellationToken: cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to abandon dead-letter message {SequenceNumber}", messageToAbandon.SequenceNumber);
                }
            }
            scannedCount += receivedMessages.Count;
        }
        return false;
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var client in _clients.Values)
        {
            await client.DisposeAsync().ConfigureAwait(false);
        }
        _clients.Clear();
    }
}
