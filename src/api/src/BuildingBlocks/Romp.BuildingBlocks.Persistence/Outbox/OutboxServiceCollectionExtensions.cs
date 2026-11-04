using Microsoft.Extensions.DependencyInjection;
using Romp.BuildingBlocks.Auditing;

namespace Romp.BuildingBlocks.Persistence.Outbox;

public static class OutboxServiceCollectionExtensions
{
    /// <summary>A module calls this once from its own RegisterServices to opt its schema's OUTB_MSG table into the shared dispatcher, naming every domain event CLR type it may write there.</summary>
    public static IServiceCollection AddOutboxModule(this IServiceCollection services, string schema, params Type[] eventTypes)
    {
        // Every module writes the audit trail through its outbox (ADR 0016), so every registration accepts it.
        services.AddSingleton(new OutboxModuleRegistration(schema, eventTypes.Contains(typeof(AuditRecorded)) ? eventTypes : [.. eventTypes, typeof(AuditRecorded)]));
        return services;
    }

    /// <summary>Called once from the host (Romp.Api's Program.cs), after every module has registered itself via <see cref="AddOutboxModule"/>.</summary>
    public static IServiceCollection AddOutboxDispatcher(this IServiceCollection services, OutboxDispatcherOptions options)
    {
        services.AddSingleton(options);
        services.AddScoped<OutboxMessageContext>();
        services.AddScoped<Inbox.InboxGuard>();
        services.AddSingleton<OutboxHealthReader>();
        services.AddSingleton<IOutboxDispatcher, OutboxDispatcher>();
        services.AddHostedService<OutboxDispatcherHostedService>();
        return services;
    }
}
