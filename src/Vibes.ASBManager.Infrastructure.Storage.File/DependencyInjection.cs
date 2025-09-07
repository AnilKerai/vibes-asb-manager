using Microsoft.Extensions.DependencyInjection;
using Vibes.ASBManager.Application.Interfaces;
using Vibes.ASBManager.Infrastructure.Storage.File.Storage;

namespace Vibes.ASBManager.Infrastructure.Storage.File;

public static class DependencyInjection
{
    public static void AddFileStorage(this IServiceCollection services, string filePath)
    {
        services.AddSingleton<IConnectionStore>(_ => new JsonConnectionStore(filePath));
    }
}