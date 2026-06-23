using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging.Abstractions;
using Vibes.ASBManager.Application.Models;
using Vibes.ASBManager.Infrastructure.AzureServiceBus.ServiceBus;

namespace Vibes.ASBManager.Tests.Integration;

// Verifies snapshot paging (roadmap A2) collects EVERY message across multiple peek pages behind a
// single receiver — the exact failure mode behind the original "DLQ only shows a couple rows" bug.
// Small fetch sizes force several pages from a modest message count. Skips when the emulator isn't
// reachable on localhost:5672.
[Collection("emulator")]
public sealed class PeekPagingTests(EmulatorFixture fixture)
{
    private const string PlainQueue = "q-plain";
    private const string Topic = "t1";
    private const string PlainSubscription = "s-plain";

    private static AzureServiceBusMessaging Messaging() => new(NullLogger<AzureServiceBusMessaging>.Instance);
    private static ServiceBusClient Client() => new(EmulatorFixture.ConnectionString);

    [SkippableFact]
    public async Task PeekQueueSnapshot_collects_every_message_across_pages()
    {
        Skip.IfNot(fixture.Available, "Service Bus emulator not reachable on localhost:5672");
        var messaging = Messaging();
        await messaging.PurgeQueueAsync(EmulatorFixture.ConnectionString, PlainQueue, maxMessages: 1000);

        await SendManyAsync(client => client.CreateSender(PlainQueue), count: 50);

        // fetchSize 20 over 50 messages => pages of 20/20/10: must not stop on the short final page.
        var snapshot = await SnapshotUntilAsync(
            () => messaging.PeekQueueSnapshotAsync(EmulatorFixture.ConnectionString, PlainQueue, target: 50, fetchSize: 20, maxEmptyPeeks: 3),
            expected: 50);

        Assert.Equal(50, snapshot.Count);
        Assert.Equal(50, snapshot.Select(m => m.SequenceNumber).Distinct().Count());

        await messaging.PurgeQueueAsync(EmulatorFixture.ConnectionString, PlainQueue, maxMessages: 1000);
    }

    [SkippableFact]
    public async Task PeekQueueDeadLetterSnapshot_collects_every_dead_letter()
    {
        Skip.IfNot(fixture.Available, "Service Bus emulator not reachable on localhost:5672");
        var messaging = Messaging();
        await messaging.PurgeQueueAsync(EmulatorFixture.ConnectionString, PlainQueue, maxMessages: 1000);
        await messaging.PurgeQueueDeadLetterAsync(EmulatorFixture.ConnectionString, PlainQueue, maxMessages: 1000);

        await SendManyAsync(client => client.CreateSender(PlainQueue), count: 24);
        await DeadLetterAllAsync(PlainQueue, count: 24);

        // fetchSize 10 over 24 dead-letters => pages of 10/10/4 — the original bug's exact shape.
        var snapshot = await SnapshotUntilAsync(
            () => messaging.PeekQueueDeadLetterSnapshotAsync(EmulatorFixture.ConnectionString, PlainQueue, target: 24, fetchSize: 10, maxEmptyPeeks: 3),
            expected: 24);

        Assert.Equal(24, snapshot.Count);
        Assert.Equal(24, snapshot.Select(m => m.SequenceNumber).Distinct().Count());

        await messaging.PurgeQueueDeadLetterAsync(EmulatorFixture.ConnectionString, PlainQueue, maxMessages: 1000);
    }

    [SkippableFact]
    public async Task PeekSubscriptionSnapshot_collects_every_message_across_pages()
    {
        Skip.IfNot(fixture.Available, "Service Bus emulator not reachable on localhost:5672");
        var messaging = Messaging();
        await messaging.PurgeSubscriptionAsync(EmulatorFixture.ConnectionString, Topic, PlainSubscription, maxMessages: 1000);

        // Topic t1 also fans out to a session-required subscription, so stamp a SessionId to keep
        // that subscription's copies valid; the plain subscription ignores it.
        await SendManyAsync(client => client.CreateSender(Topic), count: 40, sessionId: "p1");

        // fetchSize 15 over 40 messages => pages of 15/15/10.
        var snapshot = await SnapshotUntilAsync(
            () => messaging.PeekSubscriptionSnapshotAsync(EmulatorFixture.ConnectionString, Topic, PlainSubscription, target: 40, fetchSize: 15, maxEmptyPeeks: 3),
            expected: 40);

        Assert.Equal(40, snapshot.Count);
        Assert.Equal(40, snapshot.Select(m => m.SequenceNumber).Distinct().Count());

        await messaging.PurgeSubscriptionAsync(EmulatorFixture.ConnectionString, Topic, PlainSubscription, maxMessages: 1000);
    }

    // --- helpers ---

    private static async Task SendManyAsync(Func<ServiceBusClient, ServiceBusSender> senderFactory, int count, string? sessionId = null)
    {
        await using var client = Client();
        await using var sender = senderFactory(client);
        var batch = Enumerable.Range(0, count)
            .Select(i => new ServiceBusMessage($"msg-{i}") { MessageId = $"m{i}", SessionId = sessionId })
            .ToList();
        await sender.SendMessagesAsync(batch);
    }

    private static async Task DeadLetterAllAsync(string queue, int count)
    {
        await using var client = Client();
        await using var receiver = client.CreateReceiver(queue, new ServiceBusReceiverOptions { ReceiveMode = ServiceBusReceiveMode.PeekLock });
        var done = 0;
        while (done < count)
        {
            var batch = await receiver.ReceiveMessagesAsync(count - done, TimeSpan.FromSeconds(5));
            if (batch.Count == 0) break;
            foreach (var message in batch)
            {
                await receiver.DeadLetterMessageAsync(message, "test", "A2 paging test");
                done++;
            }
        }
    }

    // Peek is eventually consistent right after sends/dead-letters, so re-snapshot a few times until
    // the expected count appears. The pager's own empty-tolerance handles in-page transients.
    private static async Task<IReadOnlyList<MessagePreview>> SnapshotUntilAsync(Func<Task<IReadOnlyList<MessagePreview>>> snapshot, int expected)
    {
        IReadOnlyList<MessagePreview> result = Array.Empty<MessagePreview>();
        for (var attempt = 0; attempt < 5; attempt++)
        {
            result = await snapshot();
            if (result.Count >= expected) return result;
            await Task.Delay(300);
        }
        return result;
    }
}
