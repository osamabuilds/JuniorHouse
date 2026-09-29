using Microsoft.Extensions.DependencyInjection;
using Romp.BuildingBlocks.Persistence.Auditing;
using Romp.BuildingBlocks.Persistence.Outbox;

namespace Romp.BuildingBlocks.Persistence;

/// <summary>
/// Registers the persistence building blocks shared by every module's DbContext: the clock, the
/// (currently fixed) actor, and the two SaveChanges interceptors. Called once from Romp.Api's
/// Program.cs; each module's own RegisterServices resolves these into its AddDbContext call.
/// </summary>
public static class PersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddSharedPersistence(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<ICurrentActor, SystemCurrentActor>();
        services.AddScoped<AuditSaveChangesInterceptor>();
        services.AddScoped<OutboxSaveChangesInterceptor>();

        return services;
    }
}
