using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Vibes.ASBManager.Application.Interfaces;
using Vibes.ASBManager.Infrastructure.AzureServiceBus.ServiceBus;

namespace Vibes.ASBManager.Infrastructure.AzureServiceBus;

[ExcludeFromCodeCoverage]
public static class DependencyInjection
{
    public static void AddAzureServiceBusInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IServiceBusAdmin, AzureServiceBusAdmin>();
        services.AddSingleton<IServiceBusMessaging, AzureServiceBusMessaging>();
    }
}