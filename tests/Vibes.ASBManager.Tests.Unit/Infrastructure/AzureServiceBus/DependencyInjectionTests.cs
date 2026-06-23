using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Vibes.ASBManager.Application.Interfaces.Admin;
using Vibes.ASBManager.Application.Interfaces.Messaging;
using Vibes.ASBManager.Infrastructure.AzureServiceBus;

namespace Vibes.ASBManager.Tests.Unit.Infrastructure.AzureServiceBus;

public class DependencyInjectionTests
{
    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddAzureServiceBusInfrastructure();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Messaging_interfaces_resolve_to_one_shared_instance()
    {
        await using var provider = BuildProvider();

        var sender = provider.GetRequiredService<IMessageSender>();
        var browser = provider.GetRequiredService<IMessageBrowser>();
        var maintenance = provider.GetRequiredService<IMessageMaintenance>();
        var deadLetter = provider.GetRequiredService<IDeadLetterMaintenance>();

        Assert.Same(sender, browser);
        Assert.Same(sender, maintenance);
        Assert.Same(sender, deadLetter);
    }

    [Fact]
    public async Task Admin_interfaces_resolve_to_one_shared_instance()
    {
        await using var provider = BuildProvider();

        var queue = provider.GetRequiredService<IQueueAdmin>();
        var topic = provider.GetRequiredService<ITopicAdmin>();
        var subscription = provider.GetRequiredService<ISubscriptionAdmin>();
        var rules = provider.GetRequiredService<ISubscriptionRuleAdmin>();

        Assert.Same(queue, topic);
        Assert.Same(queue, subscription);
        Assert.Same(queue, rules);
    }

    [Fact]
    public async Task Messaging_and_admin_remain_distinct_instances()
    {
        await using var provider = BuildProvider();

        object browser = provider.GetRequiredService<IMessageBrowser>();
        object queueAdmin = provider.GetRequiredService<IQueueAdmin>();

        Assert.NotSame(browser, queueAdmin);
    }
}
