using System.Net.Sockets;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging.Abstractions;
using Vibes.ASBManager.Infrastructure.AzureServiceBus.ServiceBus;

namespace Vibes.ASBManager.Tests.Integration;

// Integration tests for the session-aware dead-letter/purge/replay behaviour (roadmap A1), run
// against the Azure Service Bus emulator. They are skipped automatically when the emulator isn't
// reachable on localhost:5672. The emulator's config defines: q-plain, q-session (RequiresSession),
// topic t1 with subscriptions s-plain and s-session (RequiresSession).
public sealed class EmulatorFixture
{
    // Data-plane connection string (AMQP, port 5672). The emulator accepts this fixed dev string.
    public const string ConnectionString =
        "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;";

    public bool Available { get; }

    public EmulatorFixture()
    {
        try
        {
            using var tcp = new TcpClient();
            Available = tcp.ConnectAsync("localhost", 5672).Wait(TimeSpan.FromSeconds(2)) && tcp.Connected;
        }
        catch
        {
            Available = false;
        }
    }
}

[CollectionDefinition("emulator")]
public sealed class EmulatorCollection : ICollectionFixture<EmulatorFixture>;

[Collection("emulator")]
public sealed class SessionOperationsTests(EmulatorFixture fixture)
{
    private const string SessionQueue = "q-session";
    private const string PlainQueue = "q-plain";
    private const string Topic = "t1";
    private const string SessionSubscription = "s-session";

    private static AzureServiceBusMessaging Messaging() => new(NullLogger<AzureServiceBusMessaging>.Instance);
    private static ServiceBusClient Client() => new(EmulatorFixture.ConnectionString);

    [SkippableFact]
    public async Task PurgeQueue_drains_a_session_enabled_queue()
    {
        Skip.IfNot(fixture.Available, "Service Bus emulator not reachable on localhost:5672");
        await using var messaging = Messaging();
        await DrainQueueAsync(messaging);

        await using (var client = Client())
        await using (var sender = client.CreateSender(SessionQueue))
        {
            await sender.SendMessageAsync(new ServiceBusMessage("a") { SessionId = "s1" });
            await sender.SendMessageAsync(new ServiceBusMessage("b") { SessionId = "s1" });
            await sender.SendMessageAsync(new ServiceBusMessage("c") { SessionId = "s2" });
        }

        var purged = await messaging.PurgeQueueAsync(EmulatorFixture.ConnectionString, SessionQueue, maxMessages: 100);

        Assert.Equal(3, purged);
        await AssertEmptyAsync(messaging, SessionQueue);
    }

    [SkippableFact]
    public async Task PurgeQueue_still_drains_a_plain_queue()
    {
        Skip.IfNot(fixture.Available, "Service Bus emulator not reachable on localhost:5672");
        await using var messaging = Messaging();
        await messaging.PurgeQueueAsync(EmulatorFixture.ConnectionString, PlainQueue, maxMessages: 100);

        await using (var client = Client())
        await using (var sender = client.CreateSender(PlainQueue))
        {
            await sender.SendMessageAsync(new ServiceBusMessage("a"));
            await sender.SendMessageAsync(new ServiceBusMessage("b"));
        }

        var purged = await messaging.PurgeQueueAsync(EmulatorFixture.ConnectionString, PlainQueue, maxMessages: 100);

        Assert.Equal(2, purged);
        await AssertEmptyAsync(messaging, PlainQueue);
    }

    [SkippableFact]
    public async Task PurgeDeadLetter_drains_the_dlq_of_a_session_queue()
    {
        Skip.IfNot(fixture.Available, "Service Bus emulator not reachable on localhost:5672");
        await using var messaging = Messaging();
        await DrainQueueAsync(messaging);

        await using (var client = Client())
        {
            await using (var sender = client.CreateSender(SessionQueue))
                await sender.SendMessageAsync(new ServiceBusMessage("dead") { SessionId = "s1" });
            await DeadLetterOneSessionMessageAsync(client, SessionQueue);
        }

        var purged = await messaging.PurgeQueueDeadLetterAsync(EmulatorFixture.ConnectionString, SessionQueue, maxMessages: 100);

        Assert.Equal(1, purged);
        await AssertEmptyAsync(messaging, SessionQueue, deadLetter: true);
    }

    [SkippableFact]
    public async Task Replay_resends_a_session_dead_letter_back_to_the_queue()
    {
        Skip.IfNot(fixture.Available, "Service Bus emulator not reachable on localhost:5672");
        await using var messaging = Messaging();
        await DrainQueueAsync(messaging);

        await using (var client = Client())
        {
            await using (var sender = client.CreateSender(SessionQueue))
                await sender.SendMessageAsync(new ServiceBusMessage("replay-me") { SessionId = "s1" });
            await DeadLetterOneSessionMessageAsync(client, SessionQueue);
        }

        // Replay must resend with the original SessionId, otherwise the session-enabled queue rejects it.
        var replayed = await messaging.ReplayQueueDeadLettersAsync(EmulatorFixture.ConnectionString, SessionQueue, maxMessages: 100);

        Assert.Equal(1, replayed);
        await AssertEmptyAsync(messaging, SessionQueue, deadLetter: true);
        await AssertCountAsync(messaging, SessionQueue, expected: 1); // back in the active queue
    }

    [SkippableFact]
    public async Task PurgeSubscription_drains_a_session_enabled_subscription()
    {
        Skip.IfNot(fixture.Available, "Service Bus emulator not reachable on localhost:5672");
        await using var messaging = Messaging();
        await messaging.PurgeSubscriptionAsync(EmulatorFixture.ConnectionString, Topic, SessionSubscription, maxMessages: 100);

        await using (var client = Client())
        await using (var sender = client.CreateSender(Topic))
        {
            await sender.SendMessageAsync(new ServiceBusMessage("a") { SessionId = "s1" });
            await sender.SendMessageAsync(new ServiceBusMessage("b") { SessionId = "s2" });
        }

        var purged = await messaging.PurgeSubscriptionAsync(EmulatorFixture.ConnectionString, Topic, SessionSubscription, maxMessages: 100);

        Assert.Equal(2, purged);
        var remaining = await messaging.PeekSubscriptionAsync(EmulatorFixture.ConnectionString, Topic, SessionSubscription, maxMessages: 50);
        Assert.Empty(remaining);
    }

    // --- helpers ---

    private static async Task DrainQueueAsync(AzureServiceBusMessaging messaging)
    {
        await messaging.PurgeQueueAsync(EmulatorFixture.ConnectionString, SessionQueue, maxMessages: 1000);
        await messaging.PurgeQueueDeadLetterAsync(EmulatorFixture.ConnectionString, SessionQueue, maxMessages: 1000);
    }

    private static async Task DeadLetterOneSessionMessageAsync(ServiceBusClient client, string queue)
    {
        await using var receiver = await client.AcceptNextSessionAsync(queue);
        var messages = await receiver.ReceiveMessagesAsync(1, TimeSpan.FromSeconds(5));
        await receiver.DeadLetterMessageAsync(messages[0], "test", "integration test");
    }

    private static async Task AssertEmptyAsync(AzureServiceBusMessaging messaging, string queue, bool deadLetter = false)
        => await AssertCountAsync(messaging, queue, expected: 0, deadLetter);

    private static async Task AssertCountAsync(AzureServiceBusMessaging messaging, string queue, int expected, bool deadLetter = false)
    {
        // Peek is eventually consistent right after a destructive operation, so poll briefly.
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var peeked = deadLetter
                ? await messaging.PeekQueueDeadLetterAsync(EmulatorFixture.ConnectionString, queue, maxMessages: 50)
                : await messaging.PeekQueueAsync(EmulatorFixture.ConnectionString, queue, maxMessages: 50);
            if (peeked.Count == expected) return;
            await Task.Delay(300);
        }

        var final = deadLetter
            ? await messaging.PeekQueueDeadLetterAsync(EmulatorFixture.ConnectionString, queue, maxMessages: 50)
            : await messaging.PeekQueueAsync(EmulatorFixture.ConnectionString, queue, maxMessages: 50);
        Assert.Equal(expected, final.Count);
    }
}
