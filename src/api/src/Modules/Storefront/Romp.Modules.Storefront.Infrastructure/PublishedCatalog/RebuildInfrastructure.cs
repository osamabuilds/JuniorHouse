using System.Threading.Channels;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Romp.BuildingBlocks.Web;
using Romp.Modules.Storefront.Application.PublishedCatalog;
using Romp.Modules.Storefront.Domain.PublishedCatalog;
using Romp.Modules.Storefront.Infrastructure.Persistence;

namespace Romp.Modules.Storefront.Infrastructure.PublishedCatalog;

/// <summary>The run ids waiting for the worker. In memory on purpose: a run lost with the process is marked failed at the next start and can be started again.</summary>
public sealed class PendingRebuilds
{
    private readonly Channel<long> _channel = Channel.CreateUnbounded<long>(new UnboundedChannelOptions { SingleReader = true });

    public ChannelReader<long> Reader => _channel.Reader;

    public void Enqueue(long runId) => _channel.Writer.TryWrite(runId);
}

/// <summary>
/// Runs rebuilds one at a time, away from any request (PCT-6). At start-up it fails the runs a dead process left "running", and starts a
/// rebuild by itself when the stored model version differs from this code's (a new column or rule needs every row recomputed).
/// </summary>
internal sealed partial class RebuildWorker(IServiceScopeFactory scopes, PendingRebuilds queue, ILogger<RebuildWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await PrepareAsync(stoppingToken);
            await foreach (var runId in queue.Reader.ReadAllAsync(stoppingToken))
            {
                await RunAsync(runId, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // shutting down
        }
    }

    private async Task PrepareAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new FailRebuildCommand(), cancellationToken);

            var db = scope.ServiceProvider.GetRequiredService<StfrDbContext>();
            var version = await db.ModelMeta.AsNoTracking().Select(m => (int?)m.ModelVersion).FirstOrDefaultAsync(cancellationToken);
            if (version is not null && version != ModelMeta.CurrentVersion)
            {
                LogAutoRebuild(version.Value, ModelMeta.CurrentVersion);
                queue.Enqueue(await sender.Send(new StartRebuildCommand("system"), cancellationToken));
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogPrepareFailed(ex);
        }
    }

    private async Task RunAsync(long runId, CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var run = await scope.ServiceProvider.GetRequiredService<ISender>().Send(new RebuildStorefrontCommand(runId), cancellationToken);
            LogRebuilt(runId, run.ProductCount, run.ChartCount, run.StockCount);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogRebuildFailed(runId, ex);
            await using var scope = scopes.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<ISender>().Send(new FailRebuildCommand(runId), CancellationToken.None);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "The storefront read model is at version {Stored}, this code builds {Current}: starting a rebuild.")]
    private partial void LogAutoRebuild(int stored, int current);

    [LoggerMessage(Level = LogLevel.Information, Message = "Storefront rebuild {RunId} finished: {Products} products, {Charts} charts, {Stock} stock rows.")]
    private partial void LogRebuilt(long runId, int products, int charts, int stock);

    [LoggerMessage(Level = LogLevel.Error, Message = "Storefront rebuild {RunId} failed; the existing rows are untouched.")]
    private partial void LogRebuildFailed(long runId, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "The storefront rebuild worker could not check the read model at start-up.")]
    private partial void LogPrepareFailed(Exception exception);
}

/// <summary>POST /api/admin/storefront/rebuild starts a rebuild and answers 202 at once; GET .../{runId} reports on it (PCT-6).</summary>
public static class StorefrontRebuildEndpoints
{
    public const string Prefix = "/api/admin/storefront/rebuild";

    public static void MapAdminStorefrontRebuildEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // Admin-only now; the narrower `storefront.operate` permission arrives with the roles of Sprint 8.
        var group = endpoints.MapSecuredGroup(Prefix, Policies.Admin).WithTags("Storefront rebuild");

        group.MapPost("/", async (HttpContext http, ISender sender, PendingRebuilds queue, CancellationToken cancellationToken) =>
            {
                var actor = http.User.Identity?.Name ?? "admin-api";
                var runId = await sender.Send(new StartRebuildCommand(actor.Length > 100 ? actor[..100] : actor), cancellationToken);
                queue.Enqueue(runId);
                return Results.Accepted($"{Prefix}/{runId}", new { runId });
            })
            .RequireRateLimiting(RateLimitPolicies.Strict);

        group.MapGet("/{runId:long}", async (long runId, ISender sender, CancellationToken cancellationToken) =>
            await sender.Send(new GetRebuildRunQuery(runId), cancellationToken) is { } run ? Results.Ok(run) : Results.NotFound());
    }
}

/// <summary>The operator verb <c>rebuild-storefront</c> (ADR 0016): the same rebuild, run inline on the server, audited as <c>operator-cli</c>.</summary>
public static class StorefrontOperator
{
    public const string RebuildVerb = "rebuild-storefront";

    public static async Task<int> RebuildAsync(IServiceProvider services, TextWriter output, CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        long? runId = null;
        try
        {
            runId = await sender.Send(new StartRebuildCommand("operator-cli"), cancellationToken);
            var run = await sender.Send(new RebuildStorefrontCommand(runId.Value), cancellationToken);
            await output.WriteLineAsync($"Rebuild {run.Id} {run.Status}: {run.ProductCount} products, {run.ChartCount} charts, {run.StockCount} stock rows.");
            return 0;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (runId is not null)
            {
                await sender.Send(new FailRebuildCommand(runId), CancellationToken.None);
            }

            await output.WriteLineAsync($"Rebuild failed: {ex.Message}");
            return 1;
        }
    }
}
