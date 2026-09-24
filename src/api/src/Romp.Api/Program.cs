using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Romp.BuildingBlocks.Modules;
using Romp.Modules.Catalog.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

IModule[] modules = [new CatalogModule()];

foreach (var module in modules)
{
    module.RegisterServices(builder.Services, builder.Configuration);
}

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

// "ready" checks (PostgreSQL, Redis) are added with their infrastructure (SCRUM-162, SCRUM-166).
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Liveness: process is up. Readiness: dependencies are reachable (SEC-22).
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });

foreach (var module in modules)
{
    module.MapEndpoints(app);
}

app.Run();

public partial class Program;
