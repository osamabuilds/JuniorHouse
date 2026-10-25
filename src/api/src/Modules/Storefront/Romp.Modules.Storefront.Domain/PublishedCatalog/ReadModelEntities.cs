using Romp.BuildingBlocks.Domain;

namespace Romp.Modules.Storefront.Domain.PublishedCatalog;

// The storefront's own read model of published products (ADR 0018, published-catalog plan). Every row is a projection: the keys are the
// producers' ids (never generated here), the only writer is the projection, and names, codes, prices and availability are copied onto the
// rows on purpose so a listing is one indexed query (read models may denormalise, CLAUDE.md). Behaviour for changing these rows arrives
// with the projection (task 10); this file is the shape the database stores.

/// <summary>STFR.PRDT_MAIN: one published product. <c>ContentVersion</c> is Catalog's content version; the highest seen wins (PCT-5).</summary>
public sealed class PublishedProduct : Entity<long>, IAuditable
{
    private PublishedProduct()
    {
        Path = string.Empty;
        Name = string.Empty;
        Description = string.Empty;
        StyleCode = string.Empty;
        GenderCode = string.Empty;
        GenderName = string.Empty;
        CategoryCode = string.Empty;
        CategoryName = string.Empty;
        AgeCode = string.Empty;
        AgeName = string.Empty;
    }

    public PublishedProduct(
        long id, long contentVersion, string path, string name, string description, string styleCode,
        string genderCode, string genderName, string categoryCode, string categoryName, string ageCode, string ageName,
        DateTimeOffset publishedAt, string? careNote)
    {
        Id = id;
        ContentVersion = contentVersion;
        Path = path;
        Name = name;
        Description = description;
        StyleCode = styleCode;
        GenderCode = genderCode;
        GenderName = genderName;
        CategoryCode = categoryCode;
        CategoryName = categoryName;
        AgeCode = ageCode;
        AgeName = ageName;
        PublishedAt = publishedAt;
        CareNote = careNote;
    }

    public long ContentVersion { get; private set; }

    public string Path { get; private set; }

    public string Name { get; private set; }

    public string Description { get; private set; }

    public string StyleCode { get; private set; }

    public string GenderCode { get; private set; }

    public string GenderName { get; private set; }

    public string CategoryCode { get; private set; }

    public string CategoryName { get; private set; }

    public string AgeCode { get; private set; }

    public string AgeName { get; private set; }

    public DateTimeOffset PublishedAt { get; private set; }

    public string? CareNote { get; private set; }

    /// <summary>Lowest price a shopper can pay (see <see cref="PriceSummary"/>); null when nothing is priced.</summary>
    public decimal? MinPrice { get; private set; }

    public decimal? MaxPrice { get; private set; }

    public bool IsOnSale { get; private set; }

    public decimal MaxDiscountPercent { get; private set; }

    /// <summary>Some variant can be bought (<see cref="ProductVisibilityResult.IsInStock"/>).</summary>
    public bool IsAvailable { get; private set; }

    /// <summary>Fit to appear in listings and search (<see cref="ProductVisibilityResult.IsListable"/>, D-2).</summary>
    public bool IsListable { get; private set; }

    /// <summary>The main image's derivatives all exist (PCT-7).</summary>
    public bool ImageReady { get; private set; }

    /// <summary>The last rebuild run that touched this row (a rebuild sweeps rows other runs left behind, PCT-6).</summary>
    public long? RebuildRunId { get; private set; }

    /// <summary>Replaces the product's own content with a newer snapshot's. Callers check <see cref="VersionedSnapshot.Replaces"/> first.</summary>
    public void Replace(
        long contentVersion, string path, string name, string description, string styleCode,
        string genderCode, string genderName, string categoryCode, string categoryName, string ageCode, string ageName,
        DateTimeOffset publishedAt, string? careNote)
    {
        ContentVersion = contentVersion;
        Path = path;
        Name = name;
        Description = description;
        StyleCode = styleCode;
        GenderCode = genderCode;
        GenderName = genderName;
        CategoryCode = categoryCode;
        CategoryName = categoryName;
        AgeCode = ageCode;
        AgeName = ageName;
        PublishedAt = publishedAt;
        CareNote = careNote;
    }

    /// <summary>Stores the values derived from the variants and images (price range, sale, availability, listability, image readiness).</summary>
    public void SetDerived(PriceSummary? price, ProductVisibilityResult visibility, bool imageReady)
    {
        ArgumentNullException.ThrowIfNull(visibility);
        MinPrice = price?.MinPrice;
        MaxPrice = price?.MaxPrice;
        IsOnSale = price?.OnSale ?? false;
        MaxDiscountPercent = price?.MaxDiscountPercent ?? 0m;
        IsAvailable = visibility.IsInStock;
        IsListable = visibility.IsListable;
        ImageReady = imageReady;
    }

    public void MarkRebuilt(long? runId) => RebuildRunId = runId;

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }
}

/// <summary>STFR.PRDT_VRNT: one size × colour. <c>Id</c> is Catalog's variant id; availability joins to it by SKU (Warehouse has its own id).</summary>
public sealed class VariantRow : Entity<long>, IAuditable
{
    private VariantRow()
    {
        ColourCode = string.Empty;
        ColourName = string.Empty;
        SizeCode = string.Empty;
        SizeName = string.Empty;
    }

    public VariantRow(
        long id, long productId, short colourId, string colourCode, string colourName,
        short sizeId, string sizeCode, string sizeName, short sizeSeq,
        string? skuCode, decimal? price, decimal? compareAtPrice, bool isSellable)
    {
        Id = id;
        ProductId = productId;
        ColourId = colourId;
        ColourCode = colourCode;
        ColourName = colourName;
        SizeId = sizeId;
        SizeCode = sizeCode;
        SizeName = sizeName;
        SizeSeq = sizeSeq;
        SkuCode = skuCode;
        SkuNormalised = NormaliseSku(skuCode);
        Price = price;
        CompareAtPrice = compareAtPrice;
        IsSellable = isSellable;
    }

    public long ProductId { get; private set; }

    public short ColourId { get; private set; }

    public string ColourCode { get; private set; }

    public string ColourName { get; private set; }

    public short SizeId { get; private set; }

    public string SizeCode { get; private set; }

    public string SizeName { get; private set; }

    /// <summary>The size lookup's sort order, so sizes list 5-6Y before 7-8Y.</summary>
    public short SizeSeq { get; private set; }

    public string? SkuCode { get; private set; }

    /// <summary>The SKU trimmed and lower-cased: what an exact SKU search matches (unique when present).</summary>
    public string? SkuNormalised { get; private set; }

    public decimal? Price { get; private set; }

    public decimal? CompareAtPrice { get; private set; }

    public bool IsSellable { get; private set; }

    /// <summary>Units available, internal only and never published (Q-PCT-2); null until a stock row for this SKU arrives (PCT-9).</summary>
    public int? AvailableQuantity { get; private set; }

    /// <summary>The Warehouse stock version applied to this row (0 until one has been).</summary>
    public long StockVersion { get; private set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }

    /// <summary>Takes the catalogue facts of a newer snapshot; the stock facts (quantity, stock version) are Warehouse's and stay.</summary>
    public void Replace(
        short colourId, string colourCode, string colourName, short sizeId, string sizeCode, string sizeName, short sizeSeq,
        string? skuCode, decimal? price, decimal? compareAtPrice, bool isSellable)
    {
        ColourId = colourId;
        ColourCode = colourCode;
        ColourName = colourName;
        SizeId = sizeId;
        SizeCode = sizeCode;
        SizeName = sizeName;
        SizeSeq = sizeSeq;
        SkuCode = skuCode;
        SkuNormalised = NormaliseSku(skuCode);
        Price = price;
        CompareAtPrice = compareAtPrice;
        IsSellable = isSellable;
    }

    /// <summary>Applies Warehouse's stock for this SKU when its version is higher than the one held (PCT-5).</summary>
    public bool ApplyStock(int quantity, long stockVersion)
    {
        if (!VersionedSnapshot.Replaces(stockVersion, StockVersion == 0 ? null : StockVersion))
        {
            return false;
        }

        AvailableQuantity = quantity;
        StockVersion = stockVersion;
        return true;
    }

    public static string? NormaliseSku(string? sku) => string.IsNullOrWhiteSpace(sku) ? null : sku.Trim().ToLowerInvariant();
}

/// <summary>STFR.PRDT_IMG: an image of a product or of one colour of it. <c>FileKey</c> addresses the original in shared storage.</summary>
public sealed class ImageRow : Entity<long>, IAuditable
{
    private ImageRow()
    {
        FileKey = string.Empty;
        AltText = string.Empty;
    }

    public ImageRow(long id, long productId, short? colourId, string fileKey, int width, int height, string altText, int sortSeq, bool isMain)
    {
        Id = id;
        ProductId = productId;
        ColourId = colourId;
        FileKey = fileKey;
        Width = width;
        Height = height;
        AltText = altText;
        SortSeq = sortSeq;
        IsMain = isMain;
    }

    /// <summary>Takes a newer snapshot's facts. A different original (<c>FileKey</c>) drops the stored content hash; the caller re-queues the derivatives.</summary>
    public void Replace(short? colourId, string fileKey, int width, int height, string altText, int sortSeq, bool isMain)
    {
        if (!string.Equals(FileKey, fileKey, StringComparison.Ordinal))
        {
            ContentHash = null;
        }

        ColourId = colourId;
        FileKey = fileKey;
        Width = width;
        Height = height;
        AltText = altText;
        SortSeq = sortSeq;
        IsMain = isMain;
    }

    public long ProductId { get; private set; }

    public short? ColourId { get; private set; }

    public string FileKey { get; private set; }

    /// <summary>The content hash of the original (<see cref="DerivativeName.HashOf"/>), set by the image worker; null until it has read the file.</summary>
    public string? ContentHash { get; private set; }

    public int Width { get; private set; }

    public int Height { get; private set; }

    public string AltText { get; private set; }

    public int SortSeq { get; private set; }

    /// <summary>The product's main image: the lowest sort order.</summary>
    public bool IsMain { get; private set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }
}

/// <summary>STFR.IMG_DRV: one resized file (width × format) of an image, and the job queue that makes it (PCT-7).</summary>
public sealed class DerivativeRow : Entity<long>, IAuditable
{
    private DerivativeRow()
    {
    }

    public DerivativeRow(long imageId, int width, DerivativeFormat format)
    {
        ImageId = imageId;
        Width = width;
        Format = format;
        Status = DerivativeStatus.Pending;
    }

    public long ImageId { get; private set; }

    public int Width { get; private set; }

    public DerivativeFormat Format { get; private set; }

    public DerivativeStatus Status { get; private set; }

    /// <summary>The generated file name (<see cref="DerivativeName"/>) once the file exists.</summary>
    public string? FileKey { get; private set; }

    public int AttemptCount { get; private set; }

    public DateTimeOffset? NextAttemptAt { get; private set; }

    public string? ErrorCode { get; private set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }
}

/// <summary>STFR.PRDT_CMPS: one fibre and its share, named for the page.</summary>
public sealed class CompositionRow : Entity<long>, IAuditable
{
    private CompositionRow()
    {
        FibreCode = string.Empty;
        FibreName = string.Empty;
    }

    public CompositionRow(long productId, string fibreCode, string fibreName, decimal percent, int sortSeq)
    {
        ProductId = productId;
        FibreCode = fibreCode;
        FibreName = fibreName;
        Percent = percent;
        SortSeq = sortSeq;
    }

    public long ProductId { get; private set; }

    public string FibreCode { get; private set; }

    public string FibreName { get; private set; }

    public decimal Percent { get; private set; }

    public int SortSeq { get; private set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }
}

/// <summary>STFR.PRDT_CARE: one care instruction with its symbol.</summary>
public sealed class CareRow : Entity<long>, IAuditable
{
    private CareRow()
    {
        CareCode = string.Empty;
        CareName = string.Empty;
        SymbolCode = string.Empty;
    }

    public CareRow(long productId, string careCode, string careName, string symbolCode, int sortSeq)
    {
        ProductId = productId;
        CareCode = careCode;
        CareName = careName;
        SymbolCode = symbolCode;
        SortSeq = sortSeq;
    }

    public long ProductId { get; private set; }

    public string CareCode { get; private set; }

    public string CareName { get; private set; }

    public string SymbolCode { get; private set; }

    public int SortSeq { get; private set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }
}

/// <summary>STFR.PRDT_COLN_MAP: the placement of a product in a collection.</summary>
public sealed class CollectionRow : Entity<long>, IAuditable
{
    private CollectionRow()
    {
        CollectionCode = string.Empty;
        CollectionName = string.Empty;
    }

    public CollectionRow(long productId, string collectionCode, string collectionName)
    {
        ProductId = productId;
        CollectionCode = collectionCode;
        CollectionName = collectionName;
    }

    public long ProductId { get; private set; }

    public string CollectionCode { get; private set; }

    public string CollectionName { get; private set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }
}

/// <summary>STFR.CHRT_MAIN: the size chart of a category and gender, as Catalog stores it. <c>ContentVersion</c> comes from Catalog's chart sequence.</summary>
public sealed class SizeChartRow : Entity<long>, IAuditable
{
    private SizeChartRow()
    {
        CategoryCode = string.Empty;
        CategoryName = string.Empty;
        GenderCode = string.Empty;
        GenderName = string.Empty;
        Name = string.Empty;
    }

    public SizeChartRow(long id, long contentVersion, string categoryCode, string categoryName, string genderCode, string genderName, string name)
    {
        Id = id;
        ContentVersion = contentVersion;
        CategoryCode = categoryCode;
        CategoryName = categoryName;
        GenderCode = genderCode;
        GenderName = genderName;
        Name = name;
    }

    public void Replace(long contentVersion, string categoryCode, string categoryName, string genderCode, string genderName, string name)
    {
        ContentVersion = contentVersion;
        CategoryCode = categoryCode;
        CategoryName = categoryName;
        GenderCode = genderCode;
        GenderName = genderName;
        Name = name;
    }

    public long ContentVersion { get; private set; }

    public string CategoryCode { get; private set; }

    public string CategoryName { get; private set; }

    public string GenderCode { get; private set; }

    public string GenderName { get; private set; }

    public string Name { get; private set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }
}

/// <summary>STFR.CHRT_LINE: one size row of a chart.</summary>
public sealed class SizeChartLineRow : Entity<long>, IAuditable
{
    private SizeChartLineRow()
    {
        SizeCode = string.Empty;
        SizeName = string.Empty;
    }

    public SizeChartLineRow(
        long chartId, string sizeCode, string sizeName, short sizeSeq, int? ageMinMonths, int? ageMaxMonths,
        decimal? heightMinCm, decimal? heightMaxCm, decimal? weightMinKg, decimal? weightMaxKg, decimal? chestCm, decimal? waistCm, decimal? lengthCm)
    {
        ChartId = chartId;
        SizeCode = sizeCode;
        SizeName = sizeName;
        SizeSeq = sizeSeq;
        AgeMinMonths = ageMinMonths;
        AgeMaxMonths = ageMaxMonths;
        HeightMinCm = heightMinCm;
        HeightMaxCm = heightMaxCm;
        WeightMinKg = weightMinKg;
        WeightMaxKg = weightMaxKg;
        ChestCm = chestCm;
        WaistCm = waistCm;
        LengthCm = lengthCm;
    }

    public long ChartId { get; private set; }

    public string SizeCode { get; private set; }

    public string SizeName { get; private set; }

    public short SizeSeq { get; private set; }

    public int? AgeMinMonths { get; private set; }

    public int? AgeMaxMonths { get; private set; }

    public decimal? HeightMinCm { get; private set; }

    public decimal? HeightMaxCm { get; private set; }

    public decimal? WeightMinKg { get; private set; }

    public decimal? WeightMaxKg { get; private set; }

    public decimal? ChestCm { get; private set; }

    public decimal? WaistCm { get; private set; }

    public decimal? LengthCm { get; private set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }
}

/// <summary>
/// STFR.AVLB_STK: the stock Warehouse reported for a SKU, kept apart from the variant rows because a stock message can arrive before its
/// product does (PCT-5). <c>Id</c> is Warehouse's (Labelling's) variant id; the row joins to a variant by SKU.
/// </summary>
public sealed class StockRow : Entity<long>, IAuditable
{
    private StockRow()
    {
        SkuCode = string.Empty;
    }

    public StockRow(long id, string skuCode, int quantity, long stockVersion)
    {
        Id = id;
        SkuCode = skuCode;
        Quantity = quantity;
        StockVersion = stockVersion;
    }

    /// <summary>Takes a newer stock report for this variant; false (nothing changed) when its version is not higher than the one held (PCT-5).</summary>
    public bool Apply(string skuCode, int quantity, long stockVersion)
    {
        if (!VersionedSnapshot.Replaces(stockVersion, StockVersion))
        {
            return false;
        }

        SkuCode = skuCode;
        Quantity = quantity;
        StockVersion = stockVersion;
        return true;
    }

    public string SkuCode { get; private set; }

    public int Quantity { get; private set; }

    public long StockVersion { get; private set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }
}

/// <summary>STFR.RBLD_RUN: one rebuild of the read model from the Catalog and Warehouse contracts (PCT-6).</summary>
public sealed class RebuildRun : Entity<long>, IAuditable
{
    private RebuildRun()
    {
        Actor = string.Empty;
    }

    public RebuildRun(DateTimeOffset startedAt, string actor)
    {
        StartedAt = startedAt;
        Actor = actor;
        Status = RebuildStatus.Running;
    }

    public RebuildStatus Status { get; private set; }

    public DateTimeOffset StartedAt { get; private set; }

    public DateTimeOffset? EndedAt { get; private set; }

    public int ProductCount { get; private set; }

    public int ChartCount { get; private set; }

    public int StockCount { get; private set; }

    /// <summary>Who started it: a staff name, or <c>operator-cli</c> (ADR 0016).</summary>
    public string Actor { get; private set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }
}

/// <summary>STFR.MODL_META: the one row describing the read model itself. A rebuild runs when <c>ModelVersion</c> differs from the code's.</summary>
public sealed class ModelMeta : Entity<short>, IAuditable
{
    /// <summary>The only row's id; the database refuses any other.</summary>
    public const short OnlyId = 1;

    private ModelMeta()
    {
    }

    /// <summary>The schema version of the read model; 0 means it has never been built.</summary>
    public int ModelVersion { get; private set; }

    /// <summary>When the lookup copies (names, codes, sort orders) on the rows were last refreshed.</summary>
    public DateTimeOffset? LookupsUpdatedAt { get; private set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }
}

/// <summary>STFR.DRV_STS_LKP ids.</summary>
public enum DerivativeStatus : short
{
    Pending = 1,
    Done = 2,
    Failed = 3,
}

/// <summary>STFR.RBLD_STS_LKP ids.</summary>
public enum RebuildStatus : short
{
    Running = 1,
    Completed = 2,
    Failed = 3,
}

/// <summary>STFR.DRV_STS_LKP</summary>
public sealed class DerivativeStatusLookup : Romp.Modules.Storefront.Domain.StorefrontSettings.StorefrontLookup<DerivativeStatus>;

/// <summary>STFR.DRV_FMT_LKP</summary>
public sealed class DerivativeFormatLookup : Romp.Modules.Storefront.Domain.StorefrontSettings.StorefrontLookup<DerivativeFormat>;

/// <summary>STFR.RBLD_STS_LKP</summary>
public sealed class RebuildStatusLookup : Romp.Modules.Storefront.Domain.StorefrontSettings.StorefrontLookup<RebuildStatus>;
