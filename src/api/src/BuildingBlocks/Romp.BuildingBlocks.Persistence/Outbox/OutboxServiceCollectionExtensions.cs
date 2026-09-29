using Microsoft.Extensions.DependencyInjection;

namespace Romp.BuildingBlocks.Persistence.Outbox;

public static class OutboxServiceCollectionExtensions
{
    /// <summary>A module calls this once from its own RegisterServices to opt its schema's OUTB_MSG table into the shared dispatcher, naming every domain event CLR type it may write there.</summary>
    public static IServiceCollection AddOutboxModule(this IServiceCollection services, string schema, params Type[] eventTypes)
    {
        services.AddSingleton(new OutboxModuleRegistration(schema, eventTypes));
        return services;
    }

    /// <summary>Called once from the host (Romp.Api's Program.cs), after every module has registered itself via <see cref="AddOutboxModule"/>.</summary>
    public static IServiceCollection AddOutboxDispatcher(this IServiceCollection services, OutboxDispatcherOptions options)
    {
        services.AddSingleton(options);
        services.AddSingleton<IOutboxDispatcher, OutboxDispatcher>();
        services.AddHostedService<OutboxDispatcherHostedService>();
        return services;
    }
}
