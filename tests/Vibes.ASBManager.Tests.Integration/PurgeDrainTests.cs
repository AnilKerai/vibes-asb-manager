using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging.Abstractions;
using Vibes.ASBManager.Infrastructure.AzureServiceBus.ServiceBus;

namespace Vibes.ASBManager.Tests.Integration;

// Verifies purge behaviour for roadmap A3: it drains to empty (no longer stops at the old hardcoded
// 1000), reports progress, and still honours an explicit cap. Counts are kept modest — the emulator's
// ReceiveAndDelete throughput is erratic at high volume under load (unlike real Service Bus), so these
// assert the mechanism deterministically rather than racing the emulator. Skips when it's unreachable.
[Collection("emulator")]
public sealed class PurgeDrainTests(EmulatorFixture fixture)
{
    private const string PlainQueue = "q-plain";

    private static AzureServiceBusMessaging Messaging() => new(NullLogger<AzureServiceBusMessaging>.Instance);

    [SkippableFact]
    public async Task PurgeQueue_drains_to_empty_and_reports_progress()
    {
        Skip.IfNot(fixture.Available, "Service Bus emulator not reachable on localhost:5672");
        await using var messaging = Messaging();
        await messaging.PurgeQueueAsync(EmulatorFixture.ConnectionString, PlainQueue); // clean slate

        const int count = 250; // spans multiple 200-message drain batches
        await SendAsync(count);

        var progress = new SyncProgress();
        // No maxMessages argument => default ceiling (well above the old 1000): drains to empty.
        var purged = await messaging.PurgeQueueAsync(EmulatorFixture.ConnectionString, PlainQueue, progress: progress);

        Assert.Equal(count, purged);
        await AssertQueueEmptyAsync(messaging);

        // Progress fired per batch, monotonically, ending at the full count.
        Assert.True(progress.Reports.Count >= 2, "expected several drain batches to report progress");
        Assert.Equal(progress.Reports.OrderBy(n => n), progress.Reports); // monotonic non-decreasing
        Assert.Equal(count, progress.Reports[^1]);
    }

    [SkippableFact]
    public async Task PurgeQueue_honours_an_explicit_cap()
    {
        Skip.IfNot(fixture.Available, "Service Bus emulator not reachable on localhost:5672");
        await using var messaging = Messaging();
        await messaging.PurgeQueueAsync(EmulatorFixture.ConnectionString, PlainQueue); // clean slate

        await SendAsync(150);

        // The cap stops the drain at exactly 50, leaving the rest — this is the knob whose default the
        // A3 change raised from 1000 to MessagingDefaults.PurgeCeiling.
        var purged = await messaging.PurgeQueueAsync(EmulatorFixture.ConnectionString, PlainQueue, maxMessages: 50);

        Assert.Equal(50, purged);
        var remaining = await messaging.PeekQueueSnapshotAsync(EmulatorFixture.ConnectionString, PlainQueue, target: 500, fetchSize: 100, maxEmptyPeeks: 5);
        Assert.Equal(100, remaining.Count);

        await messaging.PurgeQueueAsync(EmulatorFixture.ConnectionString, PlainQueue); // cleanup
    }

    private static async Task SendAsync(int count)
    {
        await using var client = new ServiceBusClient(EmulatorFixture.ConnectionString);
        await using var sender = client.CreateSender(PlainQueue);
        const int chunk = 200; // keep each batch well under the broker's max batch size
        for (var sent = 0; sent < count; sent += chunk)
        {
            var size = Math.Min(chunk, count - sent);
            var messages = Enumerable.Range(sent, size).Select(i => new ServiceBusMessage($"m{i}")).ToList();
            await sender.SendMessagesAsync(messages);
        }
    }

    private static async Task AssertQueueEmptyAsync(AzureServiceBusMessaging messaging)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var peeked = await messaging.PeekQueueAsync(EmulatorFixture.ConnectionString, PlainQueue, maxMessages: 50);
            if (peeked.Count == 0) return;
            await Task.Delay(300);
        }
        var final = await messaging.PeekQueueAsync(EmulatorFixture.ConnectionString, PlainQueue, maxMessages: 50);
        Assert.Empty(final);
    }

    // Records synchronously on the reporting thread so assertions are deterministic (unlike Progress<T>,
    // which posts asynchronously to a captured context).
    private sealed class SyncProgress : IProgress<int>
    {
        public List<int> Reports { get; } = new();
        public void Report(int value)
        {
            lock (Reports) Reports.Add(value);
        }
    }
}
