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
using Romp.Modules.Identity.Application.Abstractions;
using Romp.Modules.Identity.Application.StaffAccounts;
using Romp.Modules.Identity.Domain.StaffAccounts;
using Romp.Modules.Identity.Infrastructure.Persistence;
using Romp.Modules.Identity.Infrastructure.Roles;
using Romp.Modules.Identity.Infrastructure.StaffAccounts;

namespace Romp.Modules.Identity.Infrastructure;

/// <summary>
/// The <c>IDNT</c> module's entry point (Identity, Sprint 8, ADR 0015): staff accounts, sign-in, MFA, roles and the audit trail.
/// Sprint 8 task G2 scaffolds it; each feature registers its own services and endpoints as it lands.
/// </summary>
public sealed class IdentityModule : IModule
{
    public string Name => "Identity";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<IdntDbContext>((sp, options) => options
            .UseNpgsql(
                configuration.GetConnectionString("Postgres"),
                npgsql => npgsql.MigrationsHistoryTable("MIG_HIST", "IDNT"))
            .AddInterceptors(
                sp.GetRequiredService<AuditSaveChangesInterceptor>(),
                sp.GetRequiredService<OutboxSaveChangesInterceptor>()));

        services.AddOptions<Argon2Options>().Bind(configuration.GetSection(Argon2Options.Section)).ValidateOnStart();
        services.AddSingleton<IValidateOptions<Argon2Options>, Argon2OptionsValidator>();
        services.AddSingleton<IPasswordHasher, Argon2PasswordHasher>();
        services.AddSingleton<ITemporaryPasswordGenerator, TemporaryPasswordGenerator>();
        services.AddSingleton<ICommonPasswords, EmbeddedCommonPasswords>();
        services.AddSingleton<PasswordPolicy>();

        services.AddScoped<IIdentityDbContext>(sp => sp.GetRequiredService<IdntDbContext>());

        // Replaced by the sessions feature, which owns session rows. Until then there is nothing to end.
        services.TryAddSingleton<IUserSessionRevoker, NoSessionsYetRevoker>();
        services.TryAddSingleton<IMfaEnrolmentReset, NoMfaYetReset>();
        services.AddValidatorsFromAssembly(typeof(Application.AssemblyReference).Assembly);
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(IdentityTransactionBehavior<,>));

        // The module's events (none yet) go to the shared dispatcher; each feature adds its own as it lands.
        services.AddOutboxModule("IDNT");
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapStaffEndpoints();
        endpoints.MapRoleEndpoints();
    }
}
