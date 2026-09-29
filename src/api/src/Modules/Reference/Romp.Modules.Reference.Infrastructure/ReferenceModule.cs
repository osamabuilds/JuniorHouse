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
using Romp.BuildingBlocks.Persistence.Auditing;
using Romp.Modules.Reference.Application;
using Romp.Modules.Reference.Application.Abstractions;
using Romp.Modules.Reference.Application.Lookups;
using Romp.Modules.Reference.Contracts;
using Romp.Modules.Reference.Domain;
using Romp.Modules.Reference.Domain.Lookups;
using Romp.Modules.Reference.Domain.Lookups.Apparel;
using Romp.Modules.Reference.Domain.Lookups.PurchaseOrders;
using Romp.Modules.Reference.Domain.Lookups.Vendors;
using Romp.Modules.Reference.Infrastructure.Lookups;
using Romp.Modules.Reference.Infrastructure.Persistence;

namespace Romp.Modules.Reference.Infrastructure;

/// <summary>
/// The <c>REF</c> module's entry point (see <see cref="IModule"/> - each module is a Strategy the
/// host iterates over, so adding a module never means editing another module's code).
/// </summary>
public sealed class ReferenceModule : IModule
{
    public string Name => "Reference";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ReferenceDbContext>((sp, options) => options
            .UseNpgsql(
                configuration.GetConnectionString("Postgres"),
                npgsql => npgsql.MigrationsHistoryTable("MIG_HIST", "REF"))
            .AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>()));

        services.AddScoped<IReferenceDbContext>(sp => sp.GetRequiredService<ReferenceDbContext>());
        services.AddScoped<IPaymentTermQueries, PaymentTermQueries>();

        services.AddValidatorsFromAssembly(typeof(Application.AssemblyReference).Assembly);
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ReferenceTransactionBehavior<,>));

        RegisterLookupHandlers(services);
    }

    /// <summary>
    /// MediatR's own assembly scan only auto-registers a generic handler when its type parameters
    /// map 1:1 onto IRequestHandler&lt;,&gt;'s two slots. Ours don't - TResponse is a fixed
    /// LookupDto/none, not itself generic over TLookup - so every lookup type's handlers are
    /// registered explicitly here instead of relying on the scan finding them. Public and static
    /// so Romp.Modules.Reference.Tests can build the identical pipeline MediatR gets in production.
    /// </summary>
    public static void RegisterLookupHandlers(IServiceCollection services)
    {
        RegisterOne<SizeLookup>(services, mutable: true);
        RegisterOne<ColourLookup>(services, mutable: true);
        RegisterOne<FabricLookup>(services, mutable: true);
        RegisterOne<GenderLookup>(services, mutable: true);
        RegisterOne<AgeBracketLookup>(services, mutable: true);
        RegisterOne<CategoryLookup>(services, mutable: true);
        RegisterOne<CityLookup>(services, mutable: true);
        RegisterOne<PaymentTermLookup>(services, mutable: true);
        RegisterOne<VendorSpecialisationLookup>(services, mutable: true);
        RegisterOne<PoCancelReasonLookup>(services, mutable: true);
        RegisterOne<PoStatusLookup>(services, mutable: false);

        // Sprint 2 (SCRUM-93)
        RegisterOne<AmendmentReasonLookup>(services, mutable: true);
        RegisterOne<VendorCommChannelLookup>(services, mutable: true);
        RegisterOne<FabricResponsibilityLookup>(services, mutable: false);
        RegisterOne<RevisionStatusLookup>(services, mutable: false);
        RegisterOne<AmendmentInitiatorLookup>(services, mutable: false);
        RegisterOne<PoVendorCommTypeLookup>(services, mutable: false);
        RegisterOne<PoFileCategoryLookup>(services, mutable: false);
    }

    private static void RegisterOne<TLookup>(IServiceCollection services, bool mutable)
        where TLookup : Lookup
    {
        services.AddScoped<IRequestHandler<ListLookupQuery<TLookup>, IReadOnlyList<LookupDto>>, ListLookupQueryHandler<TLookup>>();

        if (!mutable)
        {
            return;
        }

        services.AddScoped<IRequestHandler<CreateLookupCommand<TLookup>, LookupDto>, CreateLookupCommandHandler<TLookup>>();
        services.AddScoped<IRequestHandler<UpdateLookupCommand<TLookup>, LookupDto>, UpdateLookupCommandHandler<TLookup>>();
        services.AddScoped<IRequestHandler<RetireLookupCommand<TLookup>>, RetireLookupCommandHandler<TLookup>>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapLookupType<SizeLookup>("sizes", mutable: true);
        endpoints.MapLookupType<ColourLookup>("colours", mutable: true);
        endpoints.MapLookupType<FabricLookup>("fabrics", mutable: true);
        endpoints.MapLookupType<GenderLookup>("genders", mutable: true);
        endpoints.MapLookupType<AgeBracketLookup>("age-brackets", mutable: true);
        endpoints.MapLookupType<CategoryLookup>("categories", mutable: true);
        endpoints.MapLookupType<CityLookup>("cities", mutable: true);
        endpoints.MapLookupType<PaymentTermLookup>("payment-terms", mutable: true);
        endpoints.MapLookupType<VendorSpecialisationLookup>("vendor-specialisations", mutable: true);
        endpoints.MapLookupType<PoCancelReasonLookup>("po-cancel-reasons", mutable: true);

        // System-owned (spec AC-1): read-only, no create/update/retire endpoints.
        endpoints.MapLookupType<PoStatusLookup>("po-statuses", mutable: false);

        // Sprint 2 (SCRUM-93)
        endpoints.MapLookupType<AmendmentReasonLookup>("amendment-reasons", mutable: true);
        endpoints.MapLookupType<VendorCommChannelLookup>("vendor-comm-channels", mutable: true);
        endpoints.MapLookupType<FabricResponsibilityLookup>("fabric-responsibilities", mutable: false);
        endpoints.MapLookupType<RevisionStatusLookup>("revision-statuses", mutable: false);
        endpoints.MapLookupType<AmendmentInitiatorLookup>("amendment-initiators", mutable: false);
        endpoints.MapLookupType<PoVendorCommTypeLookup>("vendor-comm-types", mutable: false);
        endpoints.MapLookupType<PoFileCategoryLookup>("po-file-categories", mutable: false);
    }
}
