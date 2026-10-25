using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Romp.BuildingBlocks.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Romp.BuildingBlocks.Modules;
using Romp.BuildingBlocks.Persistence.Auditing;
using Romp.BuildingBlocks.Persistence.Outbox;
using Romp.BuildingBlocks.Storage;
using Romp.Modules.Catalog.Contracts;
using Romp.Modules.Warehouse.Contracts;
using Romp.Modules.Storefront.Infrastructure.PublishedCatalog;
using Romp.Modules.Storefront.Application.Abstractions;
using Romp.Modules.Storefront.Contracts;
using Romp.Modules.Storefront.Infrastructure.Persistence;
using Romp.Modules.Storefront.Infrastructure.StorefrontSettings;

namespace Romp.Modules.Storefront.Infrastructure;

/// <summary>
/// The <c>STFR</c> module's entry point (Storefront, Sprint 9): admin-managed storefront settings, content pages and the published
/// snapshot the public site reads (ADR 0020). It reads other modules only through their <c>*.Contracts</c> (ADR 0002, ADR 0011).
/// </summary>
public sealed class StorefrontModule : IModule
{
    public string Name => "Storefront";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<StfrDbContext>((sp, options) => options
            .UseNpgsql(
                configuration.GetConnectionString("Postgres"),
                npgsql => npgsql.MigrationsHistoryTable("MIG_HIST", "STFR"))
            .AddInterceptors(
                sp.GetRequiredService<AuditSaveChangesInterceptor>(),
                sp.GetRequiredService<OutboxSaveChangesInterceptor>()));
        services.AddScoped<IStorefrontDbContext>(sp => sp.GetRequiredService<StfrDbContext>());
        services.AddScoped<IStorefrontReadDbContext>(sp => sp.GetRequiredService<StfrDbContext>());

        services.Configure<StorefrontOptions>(configuration.GetSection("Storefront"));
        services.AddSingleton<IExternalHostPolicy, ExternalHostPolicy>();
        services.AddSingleton<Application.PublishedCatalog.IReadModelSettings, ReadModelSettings>();
        services.AddMemoryCache();
        services.AddSingleton<IPublishedDocumentCache, PublishedDocumentCache>();
        services.AddScoped<Contracts.IStorefrontPageQueries, Application.StorefrontSettings.PublishedPageSitemapSource>();

        services.AddValidatorsFromAssembly(typeof(Application.AssemblyReference).Assembly);
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(StorefrontTransactionBehavior<,>));

        // The published event goes through the shared dispatcher; the cache drops its copy once it is committed.
        services.AddOutboxModule("STFR", typeof(StorefrontSettingsPublishedEvent));
        services.TryAdd(ServiceDescriptor.Scoped(typeof(IOutboxMessageHandler<>), typeof(LoggingOutboxMessageHandler<>)));
        services.AddScoped<IOutboxMessageHandler<StorefrontSettingsPublishedEvent>, SettingsPublishedCacheInvalidator>();

        // Published-catalog read model: fed by Catalog and Warehouse events (ADR 0018), repaired by the reconcile job.
        services.AddSingleton<PendingRebuilds>();
        services.AddHostedService<RebuildWorker>();
        services.AddHealthChecks().AddCheck<DerivativeHealthCheck>(DerivativeHealthCheck.Name, tags: ["ready"]);
        services.AddFileStorage(configuration);
        services.AddSingleton<Application.PublishedCatalog.IImageResizer, SkiaImageResizer>();
        if (configuration.GetValue("Storefront:ImageWorkerEnabled", true))
        {
            services.AddHostedService<DerivativeWorker>();
            services.AddHostedService<DerivativeSweepWorker>();
        }

        if (configuration.GetValue("Storefront:ReconcileEnabled", true))
        {
            services.AddHostedService<ReconcileWorker>();
        }

        services.AddScoped<IOutboxMessageHandler<ProductPublishedEvent>, ProductPublishedConsumer>();
        services.AddScoped<IOutboxMessageHandler<ProductChangedEvent>, ProductChangedConsumer>();
        services.AddScoped<IOutboxMessageHandler<ProductUnpublishedEvent>, ProductUnpublishedConsumer>();
        services.AddScoped<IOutboxMessageHandler<ProductArchivedEvent>, ProductArchivedConsumer>();
        services.AddScoped<IOutboxMessageHandler<SizeChartChangedEvent>, SizeChartChangedConsumer>();
        services.AddScoped<IOutboxMessageHandler<StockLevelChangedEvent>, StockLevelChangedConsumer>();
        services.AddScoped<IOutboxMessageHandler<StockPutawayConfirmedEvent>, StockPutawayConfirmedConsumer>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPublicStorefrontEndpoints();

        var services = endpoints.ServiceProvider;
        var options = services.GetRequiredService<IOptions<StorefrontOptions>>().Value;
        var environment = services.GetRequiredService<IHostEnvironment>();
        var schemes = services.GetRequiredService<IAuthenticationSchemeProvider>().GetAllSchemesAsync().GetAwaiter().GetResult();
        StorefrontAdminRoutesGuard.Verify(
            options.EnableAdminRoutes,
            RompAuthorizationExtensions.IsDevelopmentLike(environment),
            schemes.Any(s => s.Name != RompAuthorizationExtensions.DevelopmentScheme),
            environment.EnvironmentName);

        if (options.EnableAdminRoutes)
        {
            endpoints.MapAdminStorefrontSettingsEndpoints();
            endpoints.MapAdminStorefrontRebuildEndpoints();
        }
    }
}
