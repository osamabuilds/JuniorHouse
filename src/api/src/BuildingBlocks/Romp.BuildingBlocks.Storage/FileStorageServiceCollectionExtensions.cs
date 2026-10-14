using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Romp.BuildingBlocks.Storage;

public static class FileStorageServiceCollectionExtensions
{
    /// <summary>
    /// Idempotent: every module that stores files calls this, the first call wins. One shared root
    /// (<c>Vndr:PoFileStorage:RootPath</c> keeps Sprint 2's configuration key working) with generated keys, so modules never collide.
    /// </summary>
    public static IServiceCollection AddFileStorage(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddSingleton(new FileStorageOptions
        {
            RootPath = configuration.GetValue("Storage:RootPath", configuration.GetValue("Vndr:PoFileStorage:RootPath", new FileStorageOptions().RootPath))!,
        });
        services.TryAddSingleton<IFileStorage, LocalFileStorage>();
        return services;
    }
}
