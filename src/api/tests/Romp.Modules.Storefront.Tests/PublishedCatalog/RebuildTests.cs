using Microsoft.EntityFrameworkCore;
using Romp.Modules.Catalog.Contracts;
using Romp.BuildingBlocks.Domain;
using Romp.Modules.Storefront.Application.PublishedCatalog;
using Romp.Modules.Storefront.Domain.PublishedCatalog;
using Romp.Modules.Storefront.Tests.Support;
using Romp.Modules.Warehouse.Contracts;

namespace Romp.Modules.Storefront.Tests.PublishedCatalog;

/// <summary>Published-catalog task 15: <see cref="RebuildStorefrontCommand"/> rebuilds the read model from the contracts without emptying it first (PCT-6).</summary>
[Collection(StorefrontPostgresFixtureDefinition.Name)]
public sealed class RebuildTests(StorefrontPostgres postgres)
{
    private static async Task<(StorefrontHost Host, long RunId)> StartedAsync(StorefrontPostgres postgres, params PublishedProductSnapshot[] products)
    {
        var host = await StorefrontHost.CreateAsync(postgres);
        foreach (var p in products)
        {
            host.Producers.Products[p.ProductId] = p;
        }

        return (host, await host.SendAsync(new StartRebuildCommand("test")));
    }

    [Fact]
    [Trait("Spec", "PCT-6")]
    public async Task Rebuild_EmptyModel_IsFilledFromTheContracts_AndTheRunAndVersionAreRecorded()
    {
        var (host, runId) = await StartedAsync(postgres, Snapshots.Product(1), Snapshots.Product(2));
        using var _ = host;
        host.Producers.Stock[77] = new StockAvailability(77, "SKU-1-1", 4, 1);

        var run = await host.SendAsync(new RebuildStorefrontCommand(runId, PageSize: 1));

        Assert.Equal("COMPLETED", run.Status);
        Assert.Equal(2, run.ProductCount);
        Assert.Equal(1, run.StockCount);
        Assert.NotNull(run.EndedAt);
        await host.QueryAsync(async db =>
        {
            Assert.Equal(2, await db.Products.CountAsync());
            Assert.True((await db.Products.AsNoTracking().SingleAsync(p => p.Id == 1)).IsAvailable);
            Assert.Equal(ModelMeta.CurrentVersion, (await db.ModelMeta.AsNoTracking().SingleAsync()).ModelVersion);
            Assert.All(await db.Products.AsNoTracking().ToListAsync(), p => Assert.Equal(runId, p.RebuildRunId));
            return 0;
        });
    }

    [Fact]
    [Trait("Spec", "PCT-6")]
    public async Task Rebuild_RecomputesRowsEvenAtTheSameVersion()
    {
        var (host, runId) = await StartedAsync(postgres, Snapshots.Product(1, variants: [Snapshots.Variant(11, "SKU-A", price: 800m)]));
        using var _ = host;
        host.Producers.Stock[77] = new StockAvailability(77, "SKU-A", 4, 1);
        await host.SendAsync(new ReconcileCommand());
        await host.QueryAsync(db => db.Products.ExecuteUpdateAsync(s => s.SetProperty(p => p.MinPrice, (decimal?)null)));

        await host.SendAsync(new RebuildStorefrontCommand(runId));

        Assert.Equal(800m, (await host.QueryAsync(db => db.Products.AsNoTracking().SingleAsync())).MinPrice);
    }

    [Fact]
    [Trait("Spec", "PCT-6")]
    public async Task Rebuild_OldRowsKeepServing_WhileTheRunWalksTheCatalogue()
    {
        var (host, runId) = await StartedAsync(postgres, Snapshots.Product(1), Snapshots.Product(2));
        using var _ = host;
        await host.SendAsync(new ReconcileCommand());
        var seenDuringRun = new List<int>();
        host.Producers.BeforeList = async () => seenDuringRun.Add(await host.QueryAsync(db => db.Products.CountAsync()));

        await host.SendAsync(new RebuildStorefrontCommand(runId, PageSize: 1));

        Assert.NotEmpty(seenDuringRun);
        Assert.All(seenDuringRun, count => Assert.Equal(2, count));
    }

    [Fact]
    [Trait("Spec", "PCT-6")]
    public async Task Rebuild_SweepsProductsCatalogNoLongerPublishes()
    {
        var (host, runId) = await StartedAsync(postgres, Snapshots.Product(1), Snapshots.Product(2));
        using var _ = host;
        await host.SendAsync(new ReconcileCommand());
        host.Producers.Products.Remove(2);

        await host.SendAsync(new RebuildStorefrontCommand(runId));

        Assert.Equal([1L], await host.QueryAsync(db => db.Products.Select(p => p.Id).ToListAsync()));
    }

    [Fact]
    [Trait("Spec", "PCT-6")]
    public async Task Rebuild_KeepsAProductAnEventPublishedAfterTheWalkStarted()
    {
        var (host, runId) = await StartedAsync(postgres, Snapshots.Product(1), Snapshots.Product(2));
        using var _ = host;
        await host.SendAsync(new ReconcileCommand());
        host.Producers.HiddenFromLists.Add(2); // exists, but is not on the pages the walk reads

        await host.SendAsync(new RebuildStorefrontCommand(runId));

        Assert.Equal([1L, 2], await host.QueryAsync(db => db.Products.OrderBy(p => p.Id).Select(p => p.Id).ToListAsync()));
        Assert.Equal(runId, (await host.QueryAsync(db => db.Products.AsNoTracking().SingleAsync(p => p.Id == 2))).RebuildRunId);
    }

    [Fact]
    [Trait("Spec", "PCT-6")]
    public async Task Start_WhileARunIsRunning_IsRefused()
    {
        var (host, _) = await StartedAsync(postgres);
        using var _ = host;

        await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(new StartRebuildCommand("second")));
    }

    [Fact]
    [Trait("Spec", "PCT-6")]
    public async Task FailRebuild_MarksTheRunFailed_AndAFinishedRunCannotBeRebuiltAgain()
    {
        var (host, runId) = await StartedAsync(postgres);
        using var _ = host;

        Assert.Equal(1, await host.SendAsync(new FailRebuildCommand(runId)));

        Assert.Equal("FAILED", (await host.SendAsync(new GetRebuildRunQuery(runId)))!.Status);
        await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(new RebuildStorefrontCommand(runId)));
        await host.SendAsync(new StartRebuildCommand("next")); // a failed run no longer blocks a new one
    }

    [Fact]
    [Trait("Spec", "PCT-6")]
    public async Task GetRun_Unknown_IsNull()
    {
        using var host = await StorefrontHost.CreateAsync(postgres);

        Assert.Null(await host.SendAsync(new GetRebuildRunQuery(999)));
    }

    [Fact]
    [Trait("Spec", "PCT-6")]
    public async Task OperatorVerb_RebuildsInline_AsOperatorCli()
    {
        using var host = await StorefrontHost.CreateAsync(postgres);
        host.Producers.Products[1] = Snapshots.Product();

        var (exit, output) = await host.RunOperatorRebuildAsync();

        Assert.Equal(0, exit);
        Assert.Contains("COMPLETED", output);
        Assert.Equal("operator-cli", (await host.SendAsync(new GetRebuildRunQuery(1)))!.Actor);
        Assert.Equal(1, await host.QueryAsync(db => db.Products.CountAsync()));
    }
}
