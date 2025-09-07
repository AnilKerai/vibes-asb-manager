using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Vibes.ASBManager.Application.Interfaces;
using Vibes.ASBManager.Application.Interfaces.Connection;
using Vibes.ASBManager.Infrastructure.Storage.File.IO;
using Vibes.ASBManager.Infrastructure.Storage.File.Options;
using Vibes.ASBManager.Infrastructure.Storage.File.Storage;

namespace Vibes.ASBManager.Infrastructure.Storage.File;

[ExcludeFromCodeCoverage]
public static class DependencyInjection
{
    public static void AddFileStorage(this IServiceCollection services, string filePath)
    {
        services.Configure<JsonFileStorageOptions>(options => options.FilePath = filePath);
        services.AddSingleton<IFileSystem, FileSystemAdapter>();
        services.AddSingleton<IConnectionStore, JsonConnectionStore>();
    }
}