using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Romp.BuildingBlocks.Email;
using Romp.BuildingBlocks.Modules;
using Romp.BuildingBlocks.Persistence.Auditing;
using Romp.BuildingBlocks.Persistence.Outbox;
using Romp.BuildingBlocks.Web;
using Romp.Modules.Customers.Application.Abstractions;
using Romp.Modules.Customers.Application.Accounts;
using Romp.Modules.Customers.Infrastructure.Accounts;
using Romp.Modules.Customers.Infrastructure.Addresses;
using Romp.Modules.Customers.Infrastructure.Persistence;

namespace Romp.Modules.Customers.Infrastructure;

/// <summary>
/// The <c>CUST</c> module's entry point (Customers, Sprint 10): the shopper's account record, saved addresses, wishlist, newsletter
/// consent and back-in-stock requests. Real sign-in is built in the final authentication sprint; until then a development-only
/// stand-in customer is the signed-in customer (ADR 0021).
/// </summary>
public sealed class CustomersModule : IModule
{
    public string Name => "Customers";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<CustDbContext>((sp, options) => options
            .UseNpgsql(
                configuration.GetConnectionString("Postgres"),
                npgsql => npgsql.MigrationsHistoryTable("MIG_HIST", "CUST"))
            .AddInterceptors(
                sp.GetRequiredService<AuditSaveChangesInterceptor>(),
                sp.GetRequiredService<OutboxSaveChangesInterceptor>(),
                sp.GetRequiredService<AuditTrailSaveChangesInterceptor>()));

        services.AddScoped<ICustomerDbContext>(sp => sp.GetRequiredService<CustDbContext>());
        services.AddOptions<Application.CustomersOptions>().Bind(configuration.GetSection(Application.CustomersOptions.SectionName))
            .Validate(Application.CustomersOptions.IsValid, "Customers:MaxAddresses must be between 1 and 50.").ValidateOnStart();
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentCustomer, HttpContextCurrentCustomer>();
        services.AddCustomerAccounts();
        services.AddScoped<Contracts.ICustomerAddressQueries, Addresses.CustomerAddressQueries>();

        // Sign-in ports (accounts S4). The fake captcha is for Development and Testing only; anywhere else Turnstile needs its secret
        // (checked when the verifier is first used) and the email sender needs SMTP settings (Romp.BuildingBlocks.Email).
        services.AddOptions<Argon2Options>().Bind(configuration.GetSection(Argon2Options.Section)).ValidateOnStart();
        services.TryAddSingleton<IValidateOptions<Argon2Options>, Argon2OptionsValidator>();
        services.AddSingleton<IPasswordHasher, Argon2PasswordHasher>();
        services.AddOptions<TurnstileOptions>().Bind(configuration.GetSection(TurnstileOptions.Section));
        services.AddHttpClient<TurnstileCaptchaVerifier>(client => client.Timeout = TimeSpan.FromSeconds(5));
        services.AddSingleton<FakeCaptchaVerifier>();
        services.AddScoped<ICaptchaVerifier>(sp =>
        {
            if (RompAuthorizationExtensions.IsDevelopmentLike(sp.GetRequiredService<IHostEnvironment>()))
            {
                return sp.GetRequiredService<FakeCaptchaVerifier>();
            }

            if (string.IsNullOrWhiteSpace(sp.GetRequiredService<IOptions<TurnstileOptions>>().Value.SecretKey))
            {
                throw new InvalidOperationException("Customers:Turnstile:SecretKey is required outside Development and Testing.");
            }

            return sp.GetRequiredService<TurnstileCaptchaVerifier>();
        });
        services.AddEmail(configuration);

        services.AddValidatorsFromAssembly(typeof(Application.AssemblyReference).Assembly);
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(CustTransactionBehavior<,>));

        // The customer policy: a signed-in customer, as decided by the customer scheme alone (never the staff session).
        services.AddAuthorizationBuilder().AddPolicy(Policies.Customer, policy =>
        {
            policy.AddAuthenticationSchemes(StandInCustomer.Scheme);
            policy.RequireAuthenticatedUser();
        });

        // Customers' events go to the shared dispatcher; every event without a real consumer gets the placeholder logging handler.
        services.AddOutboxModule("CUST");
        services.TryAdd(ServiceDescriptor.Scoped(typeof(IOutboxMessageHandler<>), typeof(LoggingOutboxMessageHandler<>)));
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var environment = endpoints.ServiceProvider.GetRequiredService<IHostEnvironment>();
        if (StandInCustomer.IsAvailable(environment))
        {
            StandInCustomer.MapSwitch(endpoints);
        }

        endpoints.MapAccountEndpoints();
        endpoints.MapAddressEndpoints();
    }

    /// <summary>
    /// Registers the stand-in authentication scheme. Called by the host only in Development and Testing, from the same place as the staff
    /// development handler; no other environment ever has this scheme (ADR 0021).
    /// </summary>
    public static void AddStandInCustomer(IServiceCollection services) =>
        services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, StandInCustomerHandler>(StandInCustomer.Scheme, _ => { });
}
