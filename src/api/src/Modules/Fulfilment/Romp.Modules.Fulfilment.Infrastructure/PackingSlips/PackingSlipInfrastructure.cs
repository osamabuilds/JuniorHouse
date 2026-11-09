using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Romp.BuildingBlocks.Web;
using Romp.Modules.Fulfilment.Application.PackingSlips;
using Romp.Modules.Fulfilment.Application.PickLists;

namespace Romp.Modules.Fulfilment.Infrastructure.PackingSlips;

/// <summary>
/// The packing slip (packing-slips spec). Reading and printing both need <c>fulfilment.pack</c>, checked on every request. The slip is built from
/// Orders at the moment of the request and shows the recipient's name and city only (SLP-7).
/// </summary>
internal static class PackingSlipEndpoints
{
    public static void MapPackingSlipEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapSecuredGroup("/api/fulfilment/orders", Permissions.FulfilmentView).WithTags("Fulfilment")
            .WithMetadata(new RequestSizeLimitAttribute(256));

        group.MapGet("/{id:long}/slip", async (long id, ISender sender, CancellationToken cancellationToken) =>
            await sender.Send(new GetPackingSlipQuery(id), cancellationToken) is { } slip ? Results.Ok(slip) : Results.NotFound())
            .RequirePermission(Permissions.FulfilmentPack);

        group.MapPost("/{id:long}/slip/print", async (long id, ISender sender, CancellationToken cancellationToken) =>
            await sender.Send(new PrintPackingSlipCommand(id), cancellationToken) is { } result
                ? Results.Ok(new { printed = result.Printed, pending = !result.Printed, slip = result.Slip })
                : Results.NotFound()).RequirePermission(Permissions.FulfilmentPack);
    }
}

/// <summary>
/// The development printer: it records that a slip was sent and succeeds. The real device (a network or USB printer) sits behind
/// <see cref="ISlipPrinter"/> and is one of the owner's external accounts (docs/roadmap/third-party-integrations.md); until then the packer prints
/// the slip page from the browser. The slip carries no more than the screen shows, and the log line carries the order number only.
/// </summary>
public sealed partial class LoggingSlipPrinter(ILogger<LoggingSlipPrinter> logger) : ISlipPrinter
{
    public Task<bool> PrintAsync(PackingSlipDto slip, CancellationToken cancellationToken)
    {
        LogPrinted(slip.OrderNumber, slip.IsReprint);
        return Task.FromResult(true);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Packing slip for order {OrderNumber} sent to the printer (reprint: {IsReprint}).")]
    private partial void LogPrinted(string orderNumber, bool isReprint);
}

/// <summary>
/// Background upkeep: prints the slips whose printer was down (SLP-6) every 30 seconds and purges the scan log once an hour. A failure in one pass is
/// logged and the next pass tries again; it never stops the host.
/// </summary>
public sealed partial class FulfilmentMaintenanceWorker(IServiceScopeFactory scopes, ILogger<FulfilmentMaintenanceWorker> logger) : BackgroundService
{
    private static readonly TimeSpan RetryEvery = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan PurgeEvery = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(RetryEvery);
        var lastPurge = DateTimeOffset.MinValue;
        while (await WaitAsync(timer, stoppingToken))
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var sender = scope.ServiceProvider.GetRequiredService<ISender>();
                await sender.Send(new RetryPendingSlipsCommand(), stoppingToken);
                if (DateTimeOffset.UtcNow - lastPurge >= PurgeEvery)
                {
                    await sender.Send(new PurgeScanLogCommand(), stoppingToken);
                    lastPurge = DateTimeOffset.UtcNow;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogFailed(ex);
            }
        }
    }

    private static async Task<bool> WaitAsync(PeriodicTimer timer, CancellationToken stoppingToken)
    {
        try
        {
            return await timer.WaitForNextTickAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Fulfilment upkeep failed; it will try again.")]
    private partial void LogFailed(Exception exception);
}
