using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Romp.BuildingBlocks.Modules;
using Romp.BuildingBlocks.Persistence;
using Romp.Modules.Vendor.Application;
using Romp.Modules.Vendor.Contracts;
using Romp.Modules.Vendor.Domain;

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

        services.AddValidatorsFromAssembly(typeof(Romp.Modules.Vendor.Application.AssemblyReference).Assembly);
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(VendorTransactionBehavior<,>));

        // SCRUM-93 task 17 (AC-5): "configuration, not constants" (plan.md) - defaults are spec.md's
        // suggested starting point (5%/20%), overridable via Vndr:PoCommercialTerms:* config.
        services.AddSingleton(new PoCommercialTermsOptions
        {
            DefaultTolerancePercent = configuration.GetValue("Vndr:PoCommercialTerms:DefaultTolerancePercent", 5m),
            MaxTolerancePercent = configuration.GetValue("Vndr:PoCommercialTerms:MaxTolerancePercent", 20m),
        });

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
        MapVendorEndpoints(endpoints);
        MapPurchaseOrderEndpoints(endpoints);
    }

    private static void MapVendorEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/vendors").WithTags("Vendor");

        group.MapPost("/", async (CreateVendorCommand command, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(command, cancellationToken)));

        group.MapPut("/{id:long}", async (
                long id,
                UpdateVendorRequest body,
                ISender sender,
                CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(body.ToCommand(id), cancellationToken)));

        group.MapGet("/{id:long}", async (long id, ISender sender, CancellationToken cancellationToken) =>
        {
            var vendor = await sender.Send(new GetVendorByIdQuery(id), cancellationToken);
            return vendor is null ? Results.NotFound() : Results.Ok(vendor);
        });

        group.MapGet("/", async (
                string? search,
                short? cityId,
                short? specialisationId,
                ISender sender,
                CancellationToken cancellationToken,
                bool activeOnly = true) =>
            Results.Ok(await sender.Send(
                new SearchVendorsQuery(search, cityId, specialisationId, activeOnly),
                cancellationToken)));
    }

    private static void MapPurchaseOrderEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/purchase-orders").WithTags("Vendor");

        group.MapPost("/", async (CreatePurchaseOrderCommand command, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(command, cancellationToken)));

        group.MapPut("/{id:long}", async (
                long id,
                UpdatePoRequest body,
                ISender sender,
                CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(body.ToCommand(id), cancellationToken)));

        group.MapPost("/{id:long}/send", async (long id, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new SendPurchaseOrderCommand(id), cancellationToken)));

        group.MapPost("/{id:long}/acknowledge", async (long id, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new AcknowledgePurchaseOrderCommand(id), cancellationToken)));

        group.MapPost("/{id:long}/cancel", async (
                long id,
                CancelPoRequest body,
                ISender sender,
                CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new CancelPurchaseOrderCommand(id, body.CancelReasonId), cancellationToken)));

        // SCRUM-93 task 29.
        group.MapPost("/{id:long}/amendments", async (
                long id,
                CreateAmendmentRequest body,
                ISender sender,
                CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(body.ToCommand(id), cancellationToken)));

        group.MapPost("/{id:long}/amendments/{revNo}/accept", async (
                long id,
                short revNo,
                ISender sender,
                CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new DecideRevisionCommand(id, revNo, Accept: true, Note: null), cancellationToken)));

        group.MapPost("/{id:long}/amendments/{revNo}/reject", async (
                long id,
                short revNo,
                RevisionNoteRequest body,
                ISender sender,
                CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new DecideRevisionCommand(id, revNo, Accept: false, body.Note), cancellationToken)));

        group.MapPost("/{id:long}/amendments/{revNo}/withdraw", async (
                long id,
                short revNo,
                RevisionNoteRequest body,
                ISender sender,
                CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new WithdrawRevisionCommand(id, revNo, body.Note), cancellationToken)));

        group.MapGet("/{id:long}/revisions", async (long id, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new GetPurchaseOrderRevisionsQuery(id), cancellationToken)));

        // SCRUM-93 task 33. The route's id always wins over any PoId in the body.
        group.MapPost("/{id:long}/vendor-response", async (
                long id,
                RecordVendorResponseCommand body,
                ISender sender,
                CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(body with { PoId = id }, cancellationToken)));

        group.MapPost("/{id:long}/vendor-amendment-request", async (
                long id,
                RecordVendorAmendmentRequestCommand body,
                ISender sender,
                CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(body with { PoId = id }, cancellationToken)));

        group.MapGet("/{id:long}", async (long id, ISender sender, CancellationToken cancellationToken) =>
        {
            var po = await sender.Send(new GetPurchaseOrderByIdQuery(id), cancellationToken);
            return po is null ? Results.NotFound() : Results.Ok(po);
        });

        group.MapGet("/", async (
                long? vendorId,
                short? statusId,
                DateOnly? deliveryFrom,
                DateOnly? deliveryTo,
                ISender sender,
                CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(
                new SearchPurchaseOrdersQuery(vendorId, statusId, deliveryFrom, deliveryTo),
                cancellationToken)));
    }
}
