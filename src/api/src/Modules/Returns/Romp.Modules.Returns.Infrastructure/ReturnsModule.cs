using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Romp.BuildingBlocks.Modules;
using Romp.BuildingBlocks.Persistence.Auditing;
using Romp.BuildingBlocks.Persistence.Outbox;
using Romp.Modules.Returns.Application.Abstractions;
using Romp.Modules.Returns.Contracts;
using Romp.Modules.Returns.Infrastructure.Persistence;
using Romp.Modules.Returns.Infrastructure.ReverseQc;
using Romp.Modules.Shipping.Contracts;

namespace Romp.Modules.Returns.Infrastructure;

/// <summary>
/// The <c>RTRN</c> module's entry point (Returns, Sprint 17): a returned parcel becomes a case, goes through the reverse-QC gate, and only its completion
/// announces a result (ADR 0033). It starts from Shipping's status event, reads Orders and puts stock back only through their contracts (ADR 0002).
/// </summary>
public sealed class ReturnsModule : IModule
{
    public string Name => "Returns";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<RtrnDbContext>((sp, options) => options
            .UseNpgsql(
                configuration.GetConnectionString("Postgres"),
                npgsql => npgsql.MigrationsHistoryTable("MIG_HIST", "RTRN"))
            .AddInterceptors(
                sp.GetRequiredService<AuditSaveChangesInterceptor>(),
                sp.GetRequiredService<OutboxSaveChangesInterceptor>(),
                sp.GetRequiredService<AuditTrailSaveChangesInterceptor>()));
        services.AddScoped<IReturnsDbContext>(sp => sp.GetRequiredService<RtrnDbContext>());

        services.AddValidatorsFromAssembly(typeof(Application.AssemblyReference).Assembly);
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ReturnsTransactionBehavior<,>));

        services.AddOutboxModule("RTRN", typeof(ReturnQcCompletedEvent));
        services.TryAdd(ServiceDescriptor.Scoped(typeof(IOutboxMessageHandler<>), typeof(LoggingOutboxMessageHandler<>)));

        services.AddScoped<IOutboxMessageHandler<ShipmentStatusChangedEvent>, ShipmentReturnedConsumer>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints) => endpoints.MapReverseQcEndpoints();
}
