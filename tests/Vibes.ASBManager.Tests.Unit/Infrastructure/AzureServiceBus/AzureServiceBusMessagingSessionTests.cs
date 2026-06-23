using Azure.Messaging.ServiceBus;
using Vibes.ASBManager.Infrastructure.AzureServiceBus.ServiceBus;

namespace Vibes.ASBManager.Tests.Unit.Infrastructure.AzureServiceBus;

// Covers the replay-message mapping (the one session-specific bit that's pure). The session receive
// flows require a broker and are covered by the emulator-backed integration tests.
public class AzureServiceBusMessagingSessionTests
{
    [Fact]
    public void BuildReplayMessage_preserves_session_id_and_core_properties()
    {
        var source = ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: BinaryData.FromString("payload"),
            messageId: "msg-1",
            sessionId: "session-A",
            correlationId: "corr-1",
            subject: "subj",
            contentType: "application/json",
            properties: new Dictionary<string, object> { ["k"] = "v" });

        var replay = AzureServiceBusMessaging.BuildReplayMessage(source);

        Assert.Equal("session-A", replay.SessionId); // critical: session entities reject session-less sends
        Assert.Equal("msg-1", replay.MessageId);
        Assert.Equal("corr-1", replay.CorrelationId);
        Assert.Equal("subj", replay.Subject);
        Assert.Equal("application/json", replay.ContentType);
        Assert.Equal("payload", replay.Body.ToString());
        Assert.Equal("v", replay.ApplicationProperties["k"]);
    }
}
