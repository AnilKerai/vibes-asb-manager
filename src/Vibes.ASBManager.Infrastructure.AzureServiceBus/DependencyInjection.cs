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
        
        services.AddSingleton<IQueueAdmin, AzureServiceBusAdmin>();
        services.AddSingleton<ITopicAdmin, AzureServiceBusAdmin>();
        services.AddSingleton<ISubscriptionAdmin, AzureServiceBusAdmin>();
        services.AddSingleton<ISubscriptionRuleAdmin, AzureServiceBusAdmin>();
        
        services.AddSingleton<IMessageSender, AzureServiceBusMessaging>();
        services.AddSingleton<IMessageBrowser, AzureServiceBusMessaging>();
        services.AddSingleton<IMessageMaintenance, AzureServiceBusMessaging>();
        services.AddSingleton<IDeadLetterMaintenance, AzureServiceBusMessaging>();
    }
}