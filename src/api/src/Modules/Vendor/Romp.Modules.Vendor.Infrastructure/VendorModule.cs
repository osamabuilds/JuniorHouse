using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Romp.BuildingBlocks.Modules;
using Romp.BuildingBlocks.Persistence.Auditing;
using Romp.BuildingBlocks.Persistence.Outbox;
using Romp.Modules.Vendor.Application;
using Romp.Modules.Vendor.Application.Abstractions;
using Romp.Modules.Vendor.Application.Files;
using Romp.Modules.Vendor.Application.PurchaseOrders;
using Romp.Modules.Vendor.Contracts;
using Romp.Modules.Vendor.Domain;
using Romp.Modules.Vendor.Domain.PurchaseOrders;
using Romp.Modules.Vendor.Domain.Vendors;
using Romp.Modules.Vendor.Infrastructure.Files;
using Romp.Modules.Vendor.Infrastructure.Persistence;
using Romp.Modules.Vendor.Infrastructure.Revisions;
using Romp.Modules.Vendor.Infrastructure.VendorResponses;
using Romp.Modules.Vendor.Infrastructure.VendorView;
using Romp.Modules.Vendor.Infrastructure.Vendors;
using Romp.Modules.Vendor.Infrastructure.PurchaseOrders;

namespace Romp.Modules.Vendor.Infrastructure;

/// <summary>
/// The <c>VNDR</c> module's entry point (see <see cref="IModule"/> - each module is a Strategy the
/// host iterates over, so adding a module never means editing another module's code).
/// </summary>
public sealed class VendorModule : IModule
{
    public string Name => "Vendor";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<VendorDbContext>((sp, options) => options
            .UseNpgsql(
                configuration.GetConnectionString("Postgres"),
                npgsql => npgsql.MigrationsHistoryTable("MIG_HIST", "VNDR"))
            .AddInterceptors(
                sp.GetRequiredService<AuditSaveChangesInterceptor>(),
                sp.GetRequiredService<OutboxSaveChangesInterceptor>()));

        services.AddScoped<IVendorDbContext>(sp => sp.GetRequiredService<VendorDbContext>());
        services.AddScoped<IPoNumberAllocator, PoNumberAllocator>();
        services.AddScoped<IPurchaseOrderUsageQueries, PurchaseOrderUsageQueries>();
        services.AddScoped<IPurchaseOrderQueries, PurchaseOrderQueries>();

        services.AddValidatorsFromAssembly(typeof(Romp.Modules.Vendor.Application.AssemblyReference).Assembly);
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(VendorTransactionBehavior<,>));

        // SCRUM-93 task 17 (AC-5): "configuration, not constants" (plan.md) - defaults are spec.md's
        // suggested starting point (5%/20%), overridable via Vndr:PoCommercialTerms:* config.
        services.AddSingleton(new PoCommercialTermsOptions
        {
            DefaultTolerancePercent = configuration.GetValue("Vndr:PoCommercialTerms:DefaultTolerancePercent", 5m),
            MaxTolerancePercent = configuration.GetValue("Vndr:PoCommercialTerms:MaxTolerancePercent", 20m),
        });

        // SCRUM-93 task 34 (AC-45): local-disk IFileStorage for dev/Docker Compose - overridable via
        // Vndr:PoFileStorage:RootPath (e.g. a mounted volume in Docker Compose).
        services.AddSingleton(new PoFileStorageOptions
        {
            RootPath = configuration.GetValue("Vndr:PoFileStorage:RootPath", new PoFileStorageOptions().RootPath)!,
            MaxFileSizeBytes = configuration.GetValue("Vndr:PoFileStorage:MaxFileSizeBytes", new PoFileStorageOptions().MaxFileSizeBytes),
            MaxFilesPerPo = configuration.GetValue("Vndr:PoFileStorage:MaxFilesPerPo", new PoFileStorageOptions().MaxFilesPerPo),
        });
        services.AddSingleton<IFileStorage, LocalFileStorage>();

        // SCRUM-93 task 13: opt VNDR's OUTB_MSG into the shared dispatcher (SCRUM-181) and give
        // every VNDR event a placeholder handler so messages reach Processed - no module has a
        // real (DB-effecting) consumer yet, so no INBX row is needed for these (task 12's
        // convention). Sprint 3's first real consumer replaces this per event type.
        services.AddOutboxModule(
            "VNDR",
            typeof(PoCreatedEvent),
            typeof(PoSentToVendorEvent),
            typeof(PoAcknowledgedEvent),
            typeof(PoCancelledEvent),
            typeof(PoRevisionProposedEvent),
            typeof(PoRevisionPutInForceEvent),
            typeof(PoRevisionSupersededEvent),
            typeof(PoRevisionRejectedEvent),
            typeof(PoRevisionWithdrawnEvent));
        services.AddScoped(typeof(IOutboxMessageHandler<>), typeof(LoggingOutboxMessageHandler<>));
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapVendorEndpoints();
        endpoints.MapPurchaseOrderEndpoints();
        endpoints.MapRevisionEndpoints();
        endpoints.MapVendorResponseEndpoints();
        endpoints.MapVendorViewEndpoints();
        endpoints.MapPurchaseOrderFileEndpoints();
    }
}
