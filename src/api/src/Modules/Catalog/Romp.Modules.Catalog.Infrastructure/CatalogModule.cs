using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Romp.BuildingBlocks.Modules;
using Romp.BuildingBlocks.Persistence.Auditing;
using Romp.BuildingBlocks.Persistence.Outbox;
using Romp.BuildingBlocks.Storage;
using Romp.Modules.Catalog.Application;
using Romp.Modules.Catalog.Application.Abstractions;
using Romp.Modules.Catalog.Application.CompositionCare;
using Romp.Modules.Catalog.Application.ProductMedia;
using Romp.Modules.Catalog.Application.Products;
using Romp.Modules.Catalog.Application.SizeCharts;
using Romp.Modules.Catalog.Application.Urls;
using Romp.Modules.Catalog.Contracts;
using Romp.Modules.Catalog.Infrastructure.CompositionCare;
using Romp.Modules.Catalog.Infrastructure.Persistence;
using Romp.Modules.Catalog.Infrastructure.ProductMedia;
using Romp.Modules.Catalog.Infrastructure.Products;
using Romp.Modules.Catalog.Infrastructure.SizeCharts;
using Romp.Modules.Catalog.Infrastructure.Styles;
using Romp.Modules.Catalog.Infrastructure.Urls;
using Romp.Modules.Labelling.Contracts;

namespace Romp.Modules.Catalog.Infrastructure;

/// <summary>
/// The <c>CTLG</c> module's entry point (see <see cref="IModule"/> - each module is a Strategy the
/// host iterates over, so adding a module never means editing another module's code). Sprint 1: styles. Sprint 7 (Publish): products,
/// product media, size charts, composition and care, and URLs. It reads Labelling and Reference only through their
/// <c>*.Contracts</c> and tells the storefront about changes through the outbox (ADR 0002, ADR 0004, ADR 0013, ADR 0014).
/// </summary>
public sealed class CatalogModule : IModule
{
    public string Name => "Catalog";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<CatalogDbContext>((sp, options) => options
            .UseNpgsql(
                configuration.GetConnectionString("Postgres"),
                npgsql => npgsql.MigrationsHistoryTable("MIG_HIST", "CTLG"))
            .AddInterceptors(
                sp.GetRequiredService<AuditSaveChangesInterceptor>(),
                sp.GetRequiredService<OutboxSaveChangesInterceptor>()));

        services.AddScoped<ICatalogDbContext>(sp => sp.GetRequiredService<CatalogDbContext>());
        services.AddScoped<IStyleQueries, StyleQueries>();

        // "Configuration, not constants" (plan G7): validated when the host starts.
        services.AddOptions<CatalogOptions>().Bind(configuration.GetSection("Catalog")).ValidateOnStart();
        services.AddSingleton<IValidateOptions<CatalogOptions>, CatalogOptionsValidator>();
        services.AddFileStorage(configuration);

        // Each feature's public surface, used through the interface (one-way dependencies, CatalogFeatureDependencyTests).
        services.AddScoped<IProductPathBuilder, ProductPathBuilder>();
        services.AddScoped<ProductPathWriter>();
        services.AddScoped<PathResolver>();
        services.AddScoped<IProductPathQueries, ProductPathQueries>();
        services.AddScoped<IMediaGate, MediaGate>();
        services.AddScoped<ICompositionGate, CompositionGate>();
        services.AddScoped<IChartGate, ChartGate>();
        services.AddScoped<ProductAssembler>();
        services.AddScoped<IPublishedProductQueries, PublishedProductQueries>();
        services.AddScoped<ISizeChartQueries, SizeChartQueries>();
        services.AddScoped<LabelledVariantWriter>();
        services.AddScoped<ProductImageMapper>();
        services.AddScoped<CompositionCareReader>();
        services.AddScoped<SizeChartMapper>();
        services.AddScoped<PendingFileDeletes>();

        services.AddValidatorsFromAssembly(typeof(AssemblyReference).Assembly);
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(CatalogTransactionBehavior<,>));

        // Catalog's events go to the shared dispatcher; any event without a real consumer gets the placeholder logging handler.
        services.AddOutboxModule(
            "CTLG",
            typeof(ProductPublishedEvent),
            typeof(ProductUnpublishedEvent),
            typeof(ProductArchivedEvent),
            typeof(ProductChangedEvent),
            typeof(SizeChartChangedEvent));
        services.TryAdd(ServiceDescriptor.Scoped(typeof(IOutboxMessageHandler<>), typeof(LoggingOutboxMessageHandler<>)));

        // Labelling's variant events keep Catalog's copy of the SKUs and GTINs current (ADR 0014, PRD-13..15).
        services.AddScoped<IOutboxMessageHandler<VariantGtinAssignedEvent>, VariantGtinAssignedConsumer>();
        services.AddScoped<IOutboxMessageHandler<GtinVoidedEvent>, GtinVoidedConsumer>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapStyleEndpoints(Name);
        endpoints.MapProductEndpoints();
        endpoints.MapProductMediaEndpoints();
        endpoints.MapCompositionCareEndpoints();
        endpoints.MapSizeChartEndpoints();
        endpoints.MapUrlEndpoints();
    }
}
