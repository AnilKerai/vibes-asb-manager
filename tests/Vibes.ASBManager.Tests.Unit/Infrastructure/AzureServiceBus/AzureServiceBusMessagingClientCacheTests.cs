using Microsoft.Extensions.Logging.Abstractions;
using Vibes.ASBManager.Infrastructure.AzureServiceBus.ServiceBus;

namespace Vibes.ASBManager.Tests.Unit.Infrastructure.AzureServiceBus;

// Locks the contract that keeps multiple open connections/tabs isolated: one cached
// ServiceBusClient per distinct connection string, reused for the same string. B2 (client
// eviction) and B3 (connection handle) will touch this code — these guard against regressions.
// Connection strings are well-formed but fake; ServiceBusClient connects lazily, so no I/O occurs.
public class AzureServiceBusMessagingClientCacheTests
{
    private const string ConnA =
        "Endpoint=sb://ns-a.servicebus.windows.net/;SharedAccessKeyName=Root;SharedAccessKey=AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=";
    private const string ConnB =
        "Endpoint=sb://ns-b.servicebus.windows.net/;SharedAccessKeyName=Root;SharedAccessKey=BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB=";

    [Fact]
    public async Task Same_connection_string_reuses_one_client()
    {
        await using var messaging = new AzureServiceBusMessaging(NullLogger<AzureServiceBusMessaging>.Instance);

        var first = messaging.GetClient(ConnA);
        var second = messaging.GetClient(ConnA);

        Assert.Same(first, second);
    }

    [Fact]
    public async Task Distinct_connection_strings_get_isolated_clients()
    {
        await using var messaging = new AzureServiceBusMessaging(NullLogger<AzureServiceBusMessaging>.Instance);

        var clientA = messaging.GetClient(ConnA);
        var clientB = messaging.GetClient(ConnB);

        Assert.NotSame(clientA, clientB);
        Assert.Equal("ns-a.servicebus.windows.net", clientA.FullyQualifiedNamespace);
        Assert.Equal("ns-b.servicebus.windows.net", clientB.FullyQualifiedNamespace);
    }
}
