using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Romp.Modules.Catalog.Application.CompositionCare;
using Romp.Modules.Catalog.Application.ProductMedia;
using Romp.Modules.Catalog.Application.Products;
using Romp.Modules.Catalog.Domain.Products;
using Romp.Modules.Catalog.Domain.Styles;
using Romp.Modules.Catalog.Infrastructure.Persistence;
using Romp.Modules.Reference.Domain.Lookups.Apparel;
using Romp.Modules.Reference.Infrastructure.Persistence;
using Romp.Modules.Warehouse.Application.Adjustments;
using Romp.Modules.Warehouse.Application.Bins;
using Romp.Modules.Warehouse.Application.StockLedger;
using Romp.Modules.Warehouse.Domain;
using Romp.Modules.Warehouse.Domain.Adjustments;
using SkiaSharp;

namespace Romp.Api.Seeding;

/// <summary>
/// Sold items in listings, task 20 (SLD-14): a winter and a summer range for the storefront, so the shop has sold-out pieces to show.
/// Every piece is made the way staff make one: a style, a product, two pictures, composition and care, a collection, publish, and stock
/// posted to the warehouse (a sold-out piece is stock put in and taken out again, so it reads "sold out", not "not yet available").
/// The storefront read model then follows from the events, exactly as in production, and a reconcile never removes it.
/// The pictures are generated placeholders (no photographs are copied from anywhere). Development only (the host calls it behind
/// <c>Demo:SeedSampleData</c>) and idempotent: a style that already exists is left alone.
/// </summary>
internal static partial class DemoStorefrontSeeder
{
    public const string Person = "Demo Seeder";
    public const string StyleCodePrefix = "STY-DEMO-";
    public const string BinCode = "DM-01-A";

    private const long VariantIdBase = 800_000;
    private const short CottonFibreId = 1;
    private static readonly short[] CareIds = [1, 2];
    private static readonly short[] SizeIds = [4, 5, 6];
    private static readonly short[] ColourIds = [1, 6];

    /// <summary>One category of the range: its lookup code and label, the price, and twelve piece names (six a season).</summary>
    internal sealed record RangeCategory(string Code, string Name, decimal Price, short GenderId, string[] Winter, string[] Summer);

    internal static readonly RangeCategory[] Categories =
    [
        new("T-SHIRTS", "T-shirts", 1200m, 1,
            ["Thermal Crew Tee", "Long Sleeve Stripe Tee", "Brushed Cotton Tee", "Ribbed Henley Tee", "Mock Neck Tee", "Layering Tee"],
            ["Classic Crew Tee", "Pocket Tee", "Graphic Tee", "Ringer Tee", "Pastel Tee", "Everyday Tee"]),
        new("SWEATERS", "Sweaters", 2800m, 2,
            ["Chunky Knit Sweater", "Cable Knit Sweater", "Fair Isle Sweater", "Soft Wool Sweater", "Fleece Pullover", "Roll Neck Sweater"],
            ["Light Knit Sweater", "Cotton Cardigan", "Open Weave Sweater", "Cropped Cardigan", "Summer Pullover", "Linen Blend Sweater"]),
        new("JACKETS", "Jackets", 4500m, 1,
            ["Quilted Puffer Jacket", "Padded Bomber Jacket", "Hooded Parka", "Fleece Lined Jacket", "Wool Blend Coat", "Water Resistant Jacket"],
            ["Light Denim Jacket", "Windbreaker", "Cotton Bomber", "Utility Jacket", "Coach Jacket", "Seersucker Blazer"]),
        new("TROUSERS", "Trousers", 2200m, 2,
            ["Corduroy Trousers", "Lined Joggers", "Wool Blend Trousers", "Fleece Joggers", "Straight Chinos", "Cargo Trousers"],
            ["Linen Trousers", "Cotton Chinos", "Relaxed Joggers", "Pull On Trousers", "Light Cargo Trousers", "Drawstring Trousers"]),
        new("SHORTS", "Shorts", 1400m, 1,
            ["Fleece Shorts", "Corduroy Shorts", "Thermal Shorts", "Lined Bermuda Shorts", "Knit Shorts", "Wool Blend Shorts"],
            ["Linen Shorts", "Cotton Chino Shorts", "Swim Shorts", "Pull On Shorts", "Cargo Shorts", "Denim Shorts"]),
        new("DRESSES", "Dresses", 3200m, 2,
            ["Velvet Party Dress", "Knit Pinafore Dress", "Corduroy Dress", "Wool Blend Dress", "Fleece Dress", "Tartan Dress"],
            ["Floral Sun Dress", "Cotton Smock Dress", "Gingham Dress", "Tiered Dress", "Linen Shirt Dress", "Striped Jersey Dress"]),
    ];

    internal static readonly (string Code, string Name)[] Seasons = [("WINTER", "Winter"), ("SUMMER", "Summer")];

    /// <summary>How a piece is stocked: by its position in a range, a third fully stocked, a third with one size gone, a third sold out.</summary>
    internal enum Stocking
    {
        Full,
        LowAndOneSizeGone,
        SoldOut,
    }

    internal static Stocking StockingOf(int index) => (index % 3) switch
    {
        0 => Stocking.Full,
        1 => Stocking.LowAndOneSizeGone,
        _ => Stocking.SoldOut,
    };

    public static async Task SeedAsync(IServiceProvider services, ILogger logger, CancellationToken cancellationToken, int piecesPerCategory = 6)
    {
        var lookups = services.GetRequiredService<ReferenceDbContext>();
        var catalog = services.GetRequiredService<CatalogDbContext>();
        var sender = services.GetRequiredService<ISender>();

        var categoryIds = await EnsureCategoriesAsync(lookups, cancellationToken);
        var collectionIds = await EnsureSeasonsAsync(lookups, cancellationToken);
        await EnsureBinAsync(sender, logger, cancellationToken);

        var made = 0;
        int n;
        foreach (var (seasonCode, seasonName) in Seasons)
        {
            foreach (var category in Categories)
            {
                var names = seasonCode == "WINTER" ? category.Winter : category.Summer;
                for (var i = 0; i < Math.Min(names.Length, piecesPerCategory); i++)
                {
                    n = (Array.IndexOf(Seasons, (seasonCode, seasonName)) * Categories.Length * 6) + (Array.IndexOf(Categories, category) * 6) + i + 1;
                    var code = $"{StyleCodePrefix}{n:000}";
                    if (await catalog.Styles.AnyAsync(s => s.Code == code, cancellationToken))
                    {
                        continue;
                    }

                    try
                    {
                        await MakePieceAsync(
                            services, sender, catalog, n, code, seasonName, collectionIds[seasonCode], category, categoryIds[category.Code], names[i],
                            StockingOf(i), cancellationToken);
                        made++;
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        LogPieceFailed(logger, ex, code, names[i]);
                    }
                }
            }
        }

        if (made > 0)
        {
            LogPiecesMade(logger, made);
        }
    }

    private static async Task MakePieceAsync(
        IServiceProvider services, ISender sender, CatalogDbContext catalog, int n, string styleCode, string seasonName, short collectionId,
        RangeCategory category, short categoryId, string name, Stocking stocking, CancellationToken cancellationToken)
    {
        var fullName = $"{seasonName} {name}";
        var gender = category.Code == "DRESSES" ? (short)2 : (n % 2 == 0 ? (short)2 : category.GenderId);
        var ageBracket = (short)(3 + (n % 3));

        var style = new Style(styleCode, fullName, $"{seasonName} 2026", categoryId, gender, ageBracket, 1, Math.Round(category.Price * 0.4m), category.Price);
        style.SetColourSizeAndTargets(ColourIds, SizeIds, []);
        catalog.Styles.Add(style);
        await catalog.SaveChangesAsync(cancellationToken);

        // The Catalog copy of Labelling's variants and the Warehouse copy of the same variants, as the events would have made them.
        var plan = new List<(long VariantId, short ColourId, short SizeId, string Sku, string Gtin)>();
        foreach (var colourId in ColourIds)
        {
            foreach (var sizeId in SizeIds)
            {
                var variantId = VariantIdBase + (n * 100L) + (colourId * 10L) + sizeId;
                var sku = $"ROMP-DEMO-{n:000}-{colourId}-{sizeId}";
                var gtin = Gtin13.WithCheckDigit("59" + variantId.ToString("0000000000", System.Globalization.CultureInfo.InvariantCulture));
                plan.Add((variantId, colourId, sizeId, sku, gtin));
                catalog.LabelledVariants.Add(new LabelledVariant(variantId, style.Id, colourId, sizeId, sku, gtin, false));
            }
        }

        await catalog.SaveChangesAsync(cancellationToken);
        foreach (var v in plan)
        {
            await sender.Send(new RecordVariantAssignedCommand(v.VariantId, style.Id, v.ColourId, v.SizeId, v.Sku, v.Gtin), cancellationToken);
        }

        var product = await sender.Send(
            new CreateProductCommand(style.Id, fullName, $"{fullName}: a demo piece for the {seasonName.ToLowerInvariant()} range, soft and easy to wear.", gender, categoryId, ageBracket, Person),
            cancellationToken);

        await sender.Send(new UploadProductImageCommand(product.Id, Placeholder(fullName, n, 0), "front.png", null, $"{fullName}, front view", Person), cancellationToken);
        await sender.Send(new UploadProductImageCommand(product.Id, Placeholder(fullName, n, 1), "back.png", null, $"{fullName}, back view", Person), cancellationToken);

        var version = (await sender.Send(new GetProductQuery(product.Id), cancellationToken)).Version;
        var composition = await sender.Send(new SetCompositionCommand(product.Id, [new CompositionRowInput(CottonFibreId, 100m)], version, Person), cancellationToken);
        var care = await sender.Send(new SetCareCommand(product.Id, CareIds, null, composition.Version, Person), cancellationToken);
        var withCollection = await sender.Send(new SetCollectionsCommand(product.Id, [collectionId], care.Version, Person), cancellationToken);
        var published = await sender.Send(new PublishProductCommand(product.Id, withCollection.Version, Person), cancellationToken);
        if (!published.Published)
        {
            throw new InvalidOperationException("The publish gate refused: " + string.Join("; ", published.Failures.Select(f => f.Message)));
        }

        await StockAsync(sender, plan, stocking, cancellationToken);
    }

    private static async Task StockAsync(
        ISender sender, List<(long VariantId, short ColourId, short SizeId, string Sku, string Gtin)> plan, Stocking stocking, CancellationToken cancellationToken)
    {
        foreach (var v in plan)
        {
            var onShelf = stocking switch
            {
                Stocking.Full => 25,
                Stocking.LowAndOneSizeGone => v.SizeId == SizeIds[0] ? 0 : (v.SizeId == SizeIds[1] ? 2 : 12),
                _ => 0,
            };

            // Stock goes in, and a sold piece goes out again: the variant then has a stock row of zero, which the shop reads as sold out.
            var received = Math.Max(onShelf, 20);
            await PostAsync(sender, v.Gtin, received, AdjustmentReasons.FoundStock, "in", cancellationToken);
            if (received > onShelf)
            {
                await PostAsync(sender, v.Gtin, onShelf - received, AdjustmentReasons.Miscount, "out", cancellationToken);
            }
        }
    }

    private static async Task PostAsync(ISender sender, string gtin, int change, short reason, string step, CancellationToken cancellationToken) =>
        await sender.Send(
            new PostAdjustmentCommand(null, gtin, BinCode, Grades.Accepted, change, reason, "Demo data", true, StableKey($"{gtin}:{step}"), Person),
            cancellationToken);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Demo storefront: piece {Code} ({Name}) could not be made; carrying on.")]
    private static partial void LogPieceFailed(ILogger logger, Exception exception, string code, string name);

    [LoggerMessage(Level = LogLevel.Information, Message = "Demo storefront: {Count} pieces made in the winter and summer ranges.")]
    private static partial void LogPiecesMade(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Demo storefront: the demo bin {Bin} already exists.")]
    private static partial void LogBinExists(ILogger logger, Exception exception, string bin);

    /// <summary>The same text always gives the same key, so a repeat of a step is applied once.</summary>
    private static Guid StableKey(string text) =>
        new(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text))[..16]);

    private static async Task EnsureBinAsync(ISender sender, ILogger logger, CancellationToken cancellationToken)
    {
        try
        {
            await sender.Send(new CreateBinCommand("DM", "01", "A", Person), cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogBinExists(logger, ex, BinCode);
        }
    }

    private static async Task<Dictionary<string, short>> EnsureCategoriesAsync(ReferenceDbContext lookups, CancellationToken cancellationToken)
    {
        var next = (short)(await lookups.Set<CategoryLookup>().MaxAsync(c => (short?)c.SortSeq, cancellationToken) ?? 0);
        foreach (var category in Categories.Where(c => c.Code != "DRESSES"))
        {
            if (!await lookups.Set<CategoryLookup>().AnyAsync(c => c.Code == category.Code, cancellationToken))
            {
                lookups.Set<CategoryLookup>().Add(new CategoryLookup(category.Code, category.Name, null, ++next, null));
            }
        }

        await lookups.SaveChangesAsync(cancellationToken);
        var codes = Categories.Select(c => c.Code).ToList();
        return await lookups.Set<CategoryLookup>().Where(c => codes.Contains(c.Code)).ToDictionaryAsync(c => c.Code, c => c.Id, cancellationToken);
    }

    private static async Task<Dictionary<string, short>> EnsureSeasonsAsync(ReferenceDbContext lookups, CancellationToken cancellationToken)
    {
        var next = (short)(await lookups.Set<CollectionLookup>().MaxAsync(c => (short?)c.SortSeq, cancellationToken) ?? 0);
        foreach (var (code, name) in Seasons)
        {
            if (!await lookups.Set<CollectionLookup>().AnyAsync(c => c.Code == code, cancellationToken))
            {
                lookups.Set<CollectionLookup>().Add(new CollectionLookup(code, name, null, ++next));
            }
        }

        await lookups.SaveChangesAsync(cancellationToken);
        var codes = Seasons.Select(s => s.Code).ToList();
        return await lookups.Set<CollectionLookup>().Where(c => codes.Contains(c.Code)).ToDictionaryAsync(c => c.Code, c => c.Id, cancellationToken);
    }

    /// <summary>A plain 800 by 1000 picture: a soft colour chosen from the piece's number, a garment-shaped block and the name. Not a photograph.</summary>
    internal static byte[] Placeholder(string label, int n, int view)
    {
        SKColor[] palette = [new(0xE8, 0xD9, 0xC5), new(0xC9, 0xD6, 0xE3), new(0xD8, 0xE3, 0xCF), new(0xEE, 0xD0, 0xD0), new(0xDD, 0xD6, 0xEE), new(0xF1, 0xE6, 0xB8)];
        var background = palette[n % palette.Length];
        using var surface = SKSurface.Create(new SKImageInfo(800, 1000));
        var canvas = surface.Canvas;
        canvas.Clear(background);
        using var shape = new SKPaint { Color = new SKColor(0xFF, 0xFF, 0xFF, view == 0 ? (byte)150 : (byte)90), IsAntialias = true };
        canvas.DrawRoundRect(new SKRect(200, 180, 600, 820), 60, 60, shape);
        using var ink = new SKPaint { Color = new SKColor(0x22, 0x22, 0x22), IsAntialias = true };
        using var font = new SKFont { Size = 40 };
        canvas.DrawText(label, 400, 940, SKTextAlign.Center, font, ink);
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 90);
        return data.ToArray();
    }
}
