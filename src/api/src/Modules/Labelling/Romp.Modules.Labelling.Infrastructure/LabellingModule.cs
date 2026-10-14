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
using Romp.Modules.Labelling.Application.Abstractions;
using Romp.Modules.Labelling.Application.LabelWork;
using Romp.Modules.Labelling.Application.PrintJobs;
using Romp.Modules.Labelling.Application.Tags;
using Romp.Modules.Labelling.Application.Variants;
using Romp.Modules.Labelling.Contracts;
using Romp.Modules.Labelling.Infrastructure.LabelWork;
using Romp.Modules.Labelling.Infrastructure.Persistence;
using Romp.Modules.Labelling.Infrastructure.PrintJobs;
using Romp.Modules.Labelling.Infrastructure.Variants;
using Romp.Modules.Quality.Contracts;
using Romp.BuildingBlocks.Storage;

namespace Romp.Modules.Labelling.Infrastructure;

/// <summary>
/// The <c>SKU</c> module's entry point (Labelling, Sprint 5): turns QC-accepted stock into SKUs with GTIN-13 barcodes, prints the tags
/// through a retrying queue, and reprints without a new barcode. It reads other modules only through their <c>*.Contracts</c> and
/// reacts to QC's event (ADR 0002, ADR 0009, ADR 0010, ADR 0011).
/// </summary>
public sealed class LabellingModule : IModule
{
    public string Name => "Labelling";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<SkuDbContext>((sp, options) => options
            .UseNpgsql(
                configuration.GetConnectionString("Postgres"),
                npgsql => npgsql.MigrationsHistoryTable("MIG_HIST", "SKU"))
            .AddInterceptors(
                sp.GetRequiredService<AuditSaveChangesInterceptor>(),
                sp.GetRequiredService<OutboxSaveChangesInterceptor>()));
        services.AddScoped<ILabellingDbContext>(sp => sp.GetRequiredService<SkuDbContext>());

        // "Configuration, not constants" (D2, D10): validated when the host starts, so dangerous values cannot reach real labels.
        services.AddOptions<LabellingOptions>().Bind(configuration.GetSection("Labelling")).ValidateOnStart();
        services.AddSingleton<IValidateOptions<LabellingOptions>, LabellingOptionsValidator>();

        services.AddScoped<IGtinSequence, GtinSequence>();
        services.AddScoped<IJobNumberAllocator, JobNumberAllocator>();
        services.AddScoped<IRowLocks, RowLocks>();
        services.AddScoped<IPrintJobClaims, PrintJobClaims>();
        services.AddScoped<ILabelStyleLookup, ContractsLabelStyleLookup>();

        // Each feature's public surface, implemented by its own feature and used through the interface (one-way dependencies).
        services.AddScoped<IVariantAssigner, VariantAssigner>();
        services.AddScoped<IVariantReader, VariantReader>();
        services.AddScoped<IPrintProgress, PrintProgress>();
        services.AddScoped<ITagDocumentBuilder, TagDocumentBuilder>();
        services.AddScoped<LabelWorkResolver>();

        // Printers (ADR 0010): one router in front of the real (ZPL) and development (PDF preview) implementations.
        services.AddFileStorage(configuration);
        services.AddScoped<PdfPreviewLabelPrinter>();
        services.AddScoped<ZplLabelPrinter>();
        services.AddScoped<ILabelPrinter, LabelPrinterRouter>();
        services.AddScoped<PrintJobProcessor>();
        if (configuration.GetValue("Labelling:WorkerEnabled", true))
        {
            services.AddHostedService<PrintWorker>();
        }

        services.AddValidatorsFromAssembly(typeof(Application.AssemblyReference).Assembly);
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(LabellingTransactionBehavior<,>));

        // Labelling's events go to the shared dispatcher; any event without a real consumer gets the placeholder logging handler.
        services.AddOutboxModule("SKU", typeof(VariantGtinAssignedEvent), typeof(GtinVoidedEvent), typeof(TagJobPrintedEvent));
        services.TryAdd(ServiceDescriptor.Scoped(typeof(IOutboxMessageHandler<>), typeof(LoggingOutboxMessageHandler<>)));

        // Labelling's own consumer of QC's event.
        services.AddScoped<IOutboxMessageHandler<QcBatchAcceptedEvent>, QcBatchAcceptedConsumer>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapLabelWorkEndpoints();
        endpoints.MapVariantEndpoints();
        endpoints.MapPrintJobEndpoints();
    }
}
