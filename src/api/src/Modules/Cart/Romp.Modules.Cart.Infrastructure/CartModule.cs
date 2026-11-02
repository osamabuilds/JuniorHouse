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
using Romp.Modules.Cart.Application.Abstractions;
using Romp.Modules.Cart.Infrastructure.Carts;
using Romp.Modules.Cart.Infrastructure.Persistence;

namespace Romp.Modules.Cart.Infrastructure;

/// <summary>
/// The <c>CART</c> module's entry point (Cart, Sprint 11): the shopper's basket, the one server-side pricing routine, discount codes and
/// shipping rates (ADR 0024). It never stores a price (D-2) and it never reads another module's tables, only their contracts.
/// </summary>
public sealed class CartModule : IModule
{
    public string Name => "Cart";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<CartDbContext>((sp, options) => options
            .UseNpgsql(
                configuration.GetConnectionString("Postgres"),
                npgsql => npgsql.MigrationsHistoryTable("MIG_HIST", "CART"))
            .AddInterceptors(
                sp.GetRequiredService<AuditSaveChangesInterceptor>(),
                sp.GetRequiredService<OutboxSaveChangesInterceptor>(),
                sp.GetRequiredService<AuditTrailSaveChangesInterceptor>()));

        services.AddScoped<ICartDbContext>(sp => sp.GetRequiredService<CartDbContext>());
        services.AddOptions<Application.CartOptions>().Bind(configuration.GetSection(Application.CartOptions.SectionName))
            .Validate(Application.CartOptions.IsValid, "Cart:MaxPerLine, MaxLines, GuestCartDays or HoldMinutes is outside its allowed range.")
            .ValidateOnStart();

        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<Application.Carts.CartPricer>();
        services.AddScoped<Application.Carts.CartCommandSupport>();
        services.AddHttpContextAccessor();
        services.AddScoped<Carts.CartOwnerAccessor>();
        services.AddValidatorsFromAssembly(typeof(Application.AssemblyReference).Assembly);
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(CartTransactionBehavior<,>));

        // Cart's events go to the shared dispatcher; every event without a real consumer gets the placeholder logging handler.
        services.AddOutboxModule("CART");
        services.TryAdd(ServiceDescriptor.Scoped(typeof(IOutboxMessageHandler<>), typeof(LoggingOutboxMessageHandler<>)));
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapCartEndpoints();
    }
}
