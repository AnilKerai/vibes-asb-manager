using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Vibes.ASBManager.Application.Interfaces.Admin;
using Vibes.ASBManager.Application.Interfaces.Messaging;
using Vibes.ASBManager.Infrastructure.AzureServiceBus.ServiceBus;

namespace Vibes.ASBManager.Infrastructure.AzureServiceBus;

[ExcludeFromCodeCoverage]
public static class DependencyInjection
{
    public static void AddAzureServiceBusInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IRuleFormatter, RuleFormatter>();

        // Register each concrete implementation once and forward its interfaces to that same
        // instance, so a single shared ServiceBusClient pool backs all of them. Registering the
        // implementation per interface (AddSingleton<IFoo, Impl>) would create a distinct Impl
        // instance — and a distinct client cache — for every interface.
        services.AddSingleton<AzureServiceBusAdmin>();
        services.AddSingleton<IQueueAdmin>(sp => sp.GetRequiredService<AzureServiceBusAdmin>());
        services.AddSingleton<ITopicAdmin>(sp => sp.GetRequiredService<AzureServiceBusAdmin>());
        services.AddSingleton<ISubscriptionAdmin>(sp => sp.GetRequiredService<AzureServiceBusAdmin>());
        services.AddSingleton<ISubscriptionRuleAdmin>(sp => sp.GetRequiredService<AzureServiceBusAdmin>());

        services.AddSingleton<AzureServiceBusMessaging>();
        services.AddSingleton<IMessageSender>(sp => sp.GetRequiredService<AzureServiceBusMessaging>());
        services.AddSingleton<IMessageBrowser>(sp => sp.GetRequiredService<AzureServiceBusMessaging>());
        services.AddSingleton<IMessageMaintenance>(sp => sp.GetRequiredService<AzureServiceBusMessaging>());
        services.AddSingleton<IDeadLetterMaintenance>(sp => sp.GetRequiredService<AzureServiceBusMessaging>());
    }
}