using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Romp.Api;
using Romp.Api.ExceptionHandling;
using Romp.Api.Seeding;
using Romp.BuildingBlocks.Application;
using Romp.BuildingBlocks.Modules;
using Romp.BuildingBlocks.Persistence;
using Romp.BuildingBlocks.Persistence.Outbox;
using Romp.Modules.Catalog.Infrastructure;
using Romp.Modules.Catalog.Infrastructure.Persistence;
using Romp.Modules.Reference.Infrastructure;
using Romp.Modules.Reference.Infrastructure.Persistence;
using Romp.Modules.Vendor.Domain.Vendors;
using Romp.Modules.Vendor.Infrastructure;
using Romp.Modules.Vendor.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Each entry is a Strategy implementing IModule; the host only knows the interface; it never
// references another module's internals (ADR 0002).
IModule[] modules = [new ReferenceModule(), new CatalogModule(), new VendorModule()];

builder.Services.AddSharedPersistence();

// Registration order is pipeline order (outermost first): every request is logged, then
// validated - a rejected request is still logged - before reaching any module's own
// transaction behaviour (registered per-module below, closest to the handler).
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssemblies(
        typeof(Romp.Modules.Reference.Application.AssemblyReference).Assembly,
        typeof(Romp.Modules.Catalog.Application.AssemblyReference).Assembly,
        typeof(Romp.Modules.Vendor.Application.AssemblyReference).Assembly);
    cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
    cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));

    // CLAUDE.md: MediatR's commercial licence key, read from configuration/secrets, never
    // committed. Absent locally, MediatR falls back to its free-tier limits.
    var licenseKey = builder.Configuration["MediatR:LicenseKey"];
    if (!string.IsNullOrEmpty(licenseKey))
    {
        cfg.LicenseKey = licenseKey;
    }
});

foreach (var module in modules)
{
    module.RegisterServices(builder.Services, builder.Configuration);
}

// SCRUM-93 task 13: the shared dispatcher (SCRUM-181), started once every module has registered
// its own OUTB_MSG schema/event types above via AddOutboxModule.
builder.Services.AddOutboxDispatcher(new OutboxDispatcherOptions
{
    ConnectionString = builder.Configuration.GetConnectionString("Postgres")
        ?? throw new InvalidOperationException("Missing 'Postgres' connection string."),
});

builder.Services.AddExceptionHandler<ValidationExceptionHandler>();
builder.Services.AddExceptionHandler<DomainExceptionHandler>();
builder.Services.AddExceptionHandler<NotFoundExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

// Storefront and admin are separate origins (SSR/SPA hosts, different ports). No auth yet
// (CLAUDE.md: "No authentication yet"), so no credentialed requests - an explicit origin
// allowlist from config is enough; never AllowAnyOrigin once cookies/tokens are added later.
var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddPolicy("Default", policy => policy
        .WithOrigins(corsOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod());
});

// "ready" checks (PostgreSQL, Redis) are added with their infrastructure (SCRUM-162, SCRUM-166).
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors("Default");

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    // Dev-only convenience: apply each module's own migrations at startup instead of a manual
    // `dotnet ef database update` step. The three contexts are independent schemas/connections
    // with nothing shared between them, so migrating them concurrently is safe and shortens local
    // startup instead of doing it needlessly one at a time.
    using var scope = app.Services.CreateScope();
    var catalogContext = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
    var vendorContext = scope.ServiceProvider.GetRequiredService<VendorDbContext>();

    await Task.WhenAll(
        scope.ServiceProvider.GetRequiredService<ReferenceDbContext>().Database.MigrateAsync(),
        catalogContext.Database.MigrateAsync(),
        vendorContext.Database.MigrateAsync());

    // Opt-in (docker-compose sets Demo__SeedSampleData=true) so a plain `dotnet run` for backend
    // work doesn't get sample vendors/styles it didn't ask for; the "Try Sprint 1" walkthrough
    // (README) wants them there without an extra manual step.
    if (builder.Configuration.GetValue("Demo:SeedSampleData", false))
    {
        await DemoDataSeeder.SeedAsync(catalogContext, vendorContext, CancellationToken.None);
    }
}

// Liveness: process is up. Readiness: dependencies are reachable (SEC-22).
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });

foreach (var module in modules)
{
    module.MapEndpoints(app);
}

await app.RunAsync();

public partial class Program;
