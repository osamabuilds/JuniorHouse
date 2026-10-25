using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.Modules.Catalog.Contracts;
using Romp.Modules.Reference.Contracts;
using Romp.Modules.Storefront.Application.Abstractions;
using Romp.Modules.Storefront.Domain.PublishedCatalog;
using Romp.Modules.Warehouse.Contracts;

namespace Romp.Modules.Storefront.Application.PublishedCatalog;

/// <summary>What one reconcile pass changed.</summary>
public sealed record ReconcileResult(int Refreshed, int Removed, int LookupCopiesChanged)
{
    public static ReconcileResult operator +(ReconcileResult a, ReconcileResult b) =>
        new(a.Refreshed + b.Refreshed, a.Removed + b.Removed, a.LookupCopiesChanged + b.LookupCopiesChanged);
}

/// <summary>One page of a reconcile walk: how far it got, and whether it was the last page.</summary>
public sealed record ReconcilePage(long LastId, bool Done, ReconcileResult Result);

/// <summary>
/// The safety net under the event consumers (PCT-5, PCT-6): compares the read model with the Catalog and Warehouse contracts and repairs
/// whatever a lost, failed or poisoned event left behind - missing or older products, charts and stock refetched, rows the producer no
/// longer publishes removed - then refreshes the copies of lookup names. It is not itself a transaction: every repair is its own
/// command, so a long run commits as it goes and one bad row cannot undo the rest.
/// </summary>
public sealed record ReconcileCommand(int PageSize = 200) : IRequest<ReconcileResult>;

public sealed class ReconcileCommandHandler(ISender sender) : IRequestHandler<ReconcileCommand, ReconcileResult>
{
    public async Task<ReconcileResult> Handle(ReconcileCommand request, CancellationToken cancellationToken)
    {
        var total = new ReconcileResult(0, 0, 0);
        total += await WalkAsync(after => new ReconcileProductsPageCommand(after, request.PageSize), cancellationToken);
        total += await WalkAsync(after => new ReconcileChartsPageCommand(after, request.PageSize), cancellationToken);
        total += await WalkAsync(after => new ReconcileStockPageCommand(after, request.PageSize), cancellationToken);
        total += await sender.Send(new RefreshLookupCopiesCommand(), cancellationToken);
        return total;
    }

    private async Task<ReconcileResult> WalkAsync(Func<long, IRequest<ReconcilePage>> page, CancellationToken cancellationToken)
    {
        var total = new ReconcileResult(0, 0, 0);
        var after = 0L;
        while (true)
        {
            var result = await sender.Send(page(after), cancellationToken);
            total += result.Result;
            if (result.Done)
            {
                return total;
            }

            after = result.LastId;
        }
    }
}

/// <summary>Published products after <paramref name="AfterId"/>: refetch the missing or older ones, remove the ones Catalog no longer publishes in that id range.</summary>
public sealed record ReconcileProductsPageCommand(long AfterId, int Take) : IRequest<ReconcilePage>;

public sealed class ReconcileProductsPageCommandHandler(IPublishedProductQueries products, IStorefrontReadDbContext db, ISender sender)
    : IRequestHandler<ReconcileProductsPageCommand, ReconcilePage>
{
    public async Task<ReconcilePage> Handle(ReconcileProductsPageCommand request, CancellationToken cancellationToken)
    {
        var page = await products.ListVersionsAsync(request.AfterId, request.Take, cancellationToken);
        var done = page.Count < request.Take;
        var upper = done ? long.MaxValue : page[^1].ProductId;

        var stored = await db.Products.AsNoTracking()
            .Where(p => p.Id > request.AfterId && p.Id <= upper)
            .Select(p => new { p.Id, p.ContentVersion })
            .ToDictionaryAsync(p => p.Id, p => p.ContentVersion, cancellationToken);

        var refreshed = 0;
        foreach (var item in page.Where(p => VersionedSnapshot.Replaces(p.Version, stored.TryGetValue(p.ProductId, out var version) ? version : null)))
        {
            var snapshot = await products.GetSnapshotAsync(item.ProductId, cancellationToken);
            await sender.Send(new ApplyProductSnapshotCommand(item.ProductId, snapshot), cancellationToken);
            db.ClearTracker();
            refreshed++;
        }

        var published = page.Select(p => p.ProductId).ToHashSet();
        var removed = 0;
        foreach (var id in stored.Keys.Where(id => !published.Contains(id)))
        {
            await sender.Send(new ApplyProductSnapshotCommand(id, null), cancellationToken);
            db.ClearTracker();
            removed++;
        }

        return new ReconcilePage(page.Count == 0 ? request.AfterId : page[^1].ProductId, done, new ReconcileResult(refreshed, removed, 0));
    }
}

/// <summary>Size charts after <paramref name="AfterId"/>: same repair as products; the page already carries the full charts.</summary>
public sealed record ReconcileChartsPageCommand(long AfterId, int Take) : IRequest<ReconcilePage>;

public sealed class ReconcileChartsPageCommandHandler(ISizeChartQueries charts, IStorefrontReadDbContext db, ISender sender)
    : IRequestHandler<ReconcileChartsPageCommand, ReconcilePage>
{
    public async Task<ReconcilePage> Handle(ReconcileChartsPageCommand request, CancellationToken cancellationToken)
    {
        var page = await charts.ListChartsAsync(request.AfterId, request.Take, cancellationToken);
        var done = page.Count < request.Take;
        var upper = done ? long.MaxValue : page[^1].ChartId;

        var stored = await db.Charts.AsNoTracking()
            .Where(c => c.Id > request.AfterId && c.Id <= upper)
            .Select(c => new { c.Id, c.ContentVersion })
            .ToDictionaryAsync(c => c.Id, c => c.ContentVersion, cancellationToken);

        var refreshed = 0;
        foreach (var chart in page.Where(c => VersionedSnapshot.Replaces(c.Version, stored.TryGetValue(c.ChartId, out var v) ? v : null)))
        {
            await sender.Send(new ApplySizeChartCommand(chart.ChartId, chart), cancellationToken);
            db.ClearTracker();
            refreshed++;
        }

        var existing = page.Select(c => c.ChartId).ToHashSet();
        var removed = 0;
        foreach (var id in stored.Keys.Where(id => !existing.Contains(id)))
        {
            await sender.Send(new ApplySizeChartCommand(id, null), cancellationToken);
            db.ClearTracker();
            removed++;
        }

        return new ReconcilePage(page.Count == 0 ? request.AfterId : page[^1].ChartId, done, new ReconcileResult(refreshed, removed, 0));
    }
}

/// <summary>
/// Stock after <paramref name="AfterId"/> (Warehouse's variant id): apply every report newer than the one held. Nothing is removed:
/// Warehouse keeps a variant's stock row for good, and a stock row with no product yet is meant to wait (PCT-5).
/// </summary>
public sealed record ReconcileStockPageCommand(long AfterId, int Take) : IRequest<ReconcilePage>;

public sealed class ReconcileStockPageCommandHandler(IStockAvailabilityQueries stock, IStorefrontReadDbContext db, ISender sender)
    : IRequestHandler<ReconcileStockPageCommand, ReconcilePage>
{
    public async Task<ReconcilePage> Handle(ReconcileStockPageCommand request, CancellationToken cancellationToken)
    {
        var page = await stock.ListAvailabilityAsync(request.AfterId, request.Take, cancellationToken);
        var done = page.Count < request.Take;
        var ids = page.Select(s => s.VariantId).ToList();

        var stored = await db.Stock.AsNoTracking()
            .Where(s => ids.Contains(s.Id))
            .Select(s => new { s.Id, s.StockVersion })
            .ToDictionaryAsync(s => s.Id, s => s.StockVersion, cancellationToken);

        var refreshed = 0;
        foreach (var item in page.Where(s => VersionedSnapshot.Replaces(s.Version, stored.TryGetValue(s.VariantId, out var v) ? v : null)))
        {
            await sender.Send(new ApplyAvailabilityCommand(item), cancellationToken);
            db.ClearTracker();
            refreshed++;
        }

        return new ReconcilePage(page.Count == 0 ? request.AfterId : page[^1].VariantId, done, new ReconcileResult(refreshed, 0, 0));
    }
}

/// <summary>
/// Brings the copies of lookup names, codes and sort orders on the read-model rows back in line with Reference, so renaming a category
/// or reordering sizes reaches the storefront without a rebuild. Only copies that differ are written.
/// </summary>
public sealed record RefreshLookupCopiesCommand : IRequest<ReconcileResult>;

public sealed class RefreshLookupCopiesCommandHandler(IStorefrontReadDbContext db, ICatalogLookupQueries lookups, TimeProvider clock)
    : IRequestHandler<RefreshLookupCopiesCommand, ReconcileResult>
{
    public async Task<ReconcileResult> Handle(RefreshLookupCopiesCommand request, CancellationToken cancellationToken)
    {
        var genders = (await lookups.ListGendersAsync(cancellationToken)).ToDictionary(l => l.Code, l => l.Name);
        var categories = (await lookups.ListCategoriesAsync(cancellationToken)).ToDictionary(l => l.Code, l => l.Name);
        var ages = (await lookups.ListAgeBracketsAsync(cancellationToken)).ToDictionary(l => l.Code, l => l.Name);
        var collections = (await lookups.ListCollectionsAsync(cancellationToken)).ToDictionary(l => l.Code, l => l.Name);
        var sizes = (await lookups.ListSizesAsync(cancellationToken)).ToDictionary(l => l.Id);
        var colours = (await lookups.ListColoursAsync(cancellationToken)).ToDictionary(l => l.Id);

        var changed = 0;
        foreach (var (code, name) in genders)
        {
            changed += await db.Products.Where(p => p.GenderCode == code && p.GenderName != name)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.GenderName, name), cancellationToken);
            changed += await db.Charts.Where(c => c.GenderCode == code && c.GenderName != name)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.GenderName, name), cancellationToken);
        }

        foreach (var (code, name) in categories)
        {
            changed += await db.Products.Where(p => p.CategoryCode == code && p.CategoryName != name)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.CategoryName, name), cancellationToken);
            changed += await db.Charts.Where(c => c.CategoryCode == code && c.CategoryName != name)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.CategoryName, name), cancellationToken);
        }

        foreach (var (code, name) in ages)
        {
            changed += await db.Products.Where(p => p.AgeCode == code && p.AgeName != name)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.AgeName, name), cancellationToken);
        }

        foreach (var (code, name) in collections)
        {
            changed += await db.Collections.Where(c => c.CollectionCode == code && c.CollectionName != name)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.CollectionName, name), cancellationToken);
        }

        foreach (var size in sizes.Values)
        {
            changed += await db.Variants
                .Where(v => v.SizeId == size.Id && (v.SizeCode != size.Code || v.SizeName != size.Name || v.SizeSeq != size.SortSeq))
                .ExecuteUpdateAsync(
                    s => s.SetProperty(v => v.SizeCode, size.Code).SetProperty(v => v.SizeName, size.Name).SetProperty(v => v.SizeSeq, size.SortSeq),
                    cancellationToken);
            changed += await db.ChartLines
                .Where(l => l.SizeCode == size.Code && (l.SizeName != size.Name || l.SizeSeq != size.SortSeq))
                .ExecuteUpdateAsync(s => s.SetProperty(l => l.SizeName, size.Name).SetProperty(l => l.SizeSeq, size.SortSeq), cancellationToken);
        }

        foreach (var colour in colours.Values)
        {
            changed += await db.Variants
                .Where(v => v.ColourId == colour.Id && (v.ColourCode != colour.Code || v.ColourName != colour.Name))
                .ExecuteUpdateAsync(s => s.SetProperty(v => v.ColourCode, colour.Code).SetProperty(v => v.ColourName, colour.Name), cancellationToken);
        }

        var meta = await db.ModelMeta.FirstAsync(cancellationToken);
        meta.MarkLookupsRefreshed(clock.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);
        db.ClearTracker();
        return new ReconcileResult(0, 0, changed);
    }
}
