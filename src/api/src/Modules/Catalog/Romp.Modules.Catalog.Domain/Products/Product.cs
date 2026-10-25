using Romp.BuildingBlocks.Domain;
using Romp.Modules.Catalog.Contracts;

namespace Romp.Modules.Catalog.Domain.Products;

/// <summary>A placement of a product in a collection. Maps to CTLG.PRDT_COLN_MAP (PRD-12).</summary>
public sealed class ProductCollectionLink : Entity<long>, IAuditable
{
    private ProductCollectionLink()
    {
    }

    internal ProductCollectionLink(short collectionId) => CollectionId = collectionId;

    public long ProductId { get; private set; }

    public short CollectionId { get; private set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }
}

/// <summary>The path a product had before <see cref="Product.ChangePath"/> and the one it has now.</summary>
public sealed record PathChange(short OldGenderId, short OldCategoryId, string OldSlug, short NewGenderId, short NewCategoryId, string NewSlug)
{
    public bool Changed => OldGenderId != NewGenderId || OldCategoryId != NewCategoryId || OldSlug != NewSlug;
}

/// <summary>
/// A product: the storefront record a style becomes (FR-53, SCRUM-78). One per style (a unique index backs this). Draft until the
/// publish gate passes; Published products are sellable; Unpublished ones come off sale but stay in history; Archived is final
/// (PRD-3, PRD-6). The product never mints SKUs or GTINs (D1). Maps to CTLG.PRDT_MAIN.
/// </summary>
public sealed class Product : AggregateRoot<long>, IAuditable
{
    private readonly List<PlannedVariant> _variants = [];
    private readonly List<ProductCollectionLink> _collections = [];

    private Product()
    {
        Name = string.Empty;
        Description = string.Empty;
        Slug = string.Empty;
    }

    private Product(long styleId, string name, string description, short genderId, short categoryId, short ageBracketId, string slug)
    {
        StyleId = styleId;
        Name = name.Trim();
        Description = description.Trim();
        GenderId = genderId;
        CategoryId = categoryId;
        AgeBracketId = ageBracketId;
        Slug = slug;
        Status = ProductStatus.Draft;
    }

    public long StyleId { get; private set; }

    public string Name { get; private set; }

    public string Description { get; private set; }

    public short GenderId { get; private set; }

    public short CategoryId { get; private set; }

    public short AgeBracketId { get; private set; }

    /// <summary>The id of the status row (<c>PRDT_STS_LKP</c>); <see cref="Status"/> is the same value as the enum.</summary>
    public short StatusId { get; private set; }

    public ProductStatus Status
    {
        get => (ProductStatus)StatusId;
        private set => StatusId = (short)value;
    }

    public string Slug { get; private set; }

    /// <summary>A short free-text care note shown with the care symbols (SCRUM-29).</summary>
    public string? CareNote { get; private set; }

    public DateTimeOffset? PublishedAt { get; private set; }

    public string? PublishedBy { get; private set; }

    /// <summary>
    /// Goes up by exactly one, in the same save, with every change that raises a product event. The storefront read model keeps the
    /// highest version it has seen, so a duplicate or out-of-order event can never overwrite newer data (PCT-5). Not the concurrency token.
    /// </summary>
    public long ContentVersion { get; private set; } = 1;

    public IReadOnlyCollection<PlannedVariant> Variants => _variants.AsReadOnly();

    public IReadOnlyCollection<ProductCollectionLink> Collections => _collections.AsReadOnly();

    public const int MaxCareNoteLength = 500;

    /// <summary>The first problem with a care note (plain text, at most <see cref="MaxCareNoteLength"/> characters), or null.</summary>
    public static string? CheckCareNote(string? note)
    {
        if (string.IsNullOrWhiteSpace(note))
        {
            return null;
        }

        if (note.Trim().Length > MaxCareNoteLength)
        {
            return $"The care note can be at most {MaxCareNoteLength} characters.";
        }

        return note.Contains('<', StringComparison.Ordinal) || note.Contains('>', StringComparison.Ordinal)
            ? "The care note is plain text: remove the < and > characters."
            : null;
    }

    /// <summary>Sets the free-text care note shown beside the care symbols (SCRUM-29); blank clears it.</summary>
    public void SetCareNote(string? note)
    {
        EnsureEditable();
        var problem = CheckCareNote(note);
        if (problem is not null)
        {
            throw new DomainException(problem);
        }

        CareNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }

    /// <summary>
    /// Starts a product for a style (PRD-1): one planned variant for every colour × size, priced from the style's target retail price
    /// when it has one (Q-PRD-9).
    /// </summary>
    public static Product Create(
        long styleId,
        string name,
        string description,
        short genderId,
        short categoryId,
        short ageBracketId,
        string slug,
        IEnumerable<short> colourIds,
        IEnumerable<short> sizeIds,
        decimal? defaultPrice)
    {
        var product = new Product(styleId, name, description, genderId, categoryId, ageBracketId, slug);
        var sizes = sizeIds.Distinct().ToList();
        foreach (var colour in colourIds.Distinct())
        {
            foreach (var size in sizes)
            {
                product._variants.Add(new PlannedVariant(colour, size, defaultPrice));
            }
        }

        return product;
    }

    /// <summary>
    /// Edits the descriptive content (PRD-5 happens at the database through the concurrency token). Gender and category are part of the
    /// public path, so on a Published product they change only through <see cref="ChangePath"/> (ADR 0013).
    /// </summary>
    public void UpdateContent(string name, string description, short genderId, short categoryId, short ageBracketId, string? careNote)
    {
        EnsureEditable();
        if (Status == ProductStatus.Published && (genderId != GenderId || categoryId != CategoryId))
        {
            throw new DomainException("Gender and category are part of the product's URL. Change them on the URL tab, which keeps the old address working.");
        }

        Name = name.Trim();
        Description = description.Trim();
        GenderId = genderId;
        CategoryId = categoryId;
        AgeBracketId = ageBracketId;
        CareNote = string.IsNullOrWhiteSpace(careNote) ? null : careNote.Trim();
    }

    /// <summary>
    /// Moves the product to a new path (URL-1..3). Allowed in every state except Archived. The caller records the old path in the
    /// slug history; this returns what changed so it can.
    /// </summary>
    public PathChange ChangePath(short genderId, short categoryId, string slug)
    {
        EnsureEditable();
        var change = new PathChange(GenderId, CategoryId, Slug, genderId, categoryId, slug);
        GenderId = genderId;
        CategoryId = categoryId;
        Slug = slug;
        return change;
    }

    /// <summary>Sets the price of planned variants by colour × size (PRD-2, PRD-7, PRD-8) and returns the history rows to record.</summary>
    public IReadOnlyList<PriceHistoryEntry> SetPrices(IEnumerable<(short ColourId, short SizeId, decimal Price, decimal? CompareAtPrice)> prices, string person)
    {
        EnsureEditable();
        var entries = new List<PriceHistoryEntry>();
        foreach (var (colourId, sizeId, price, compareAt) in prices)
        {
            var variant = _variants.FirstOrDefault(v => v.ColourId == colourId && v.SizeId == sizeId)
                ?? throw new DomainException("That size and colour is not one of this product's variants.");
            var entry = variant.SetPrice(price, compareAt, person);
            if (entry is not null)
            {
                entries.Add(entry);
            }
        }

        return entries;
    }

    /// <summary>Replaces the product's collections (PRD-12): many collections, one category.</summary>
    public void SetCollections(IEnumerable<short> collectionIds)
    {
        EnsureEditable();
        var wanted = collectionIds.Distinct().ToHashSet();
        _collections.RemoveAll(c => !wanted.Contains(c.CollectionId));
        foreach (var id in wanted.Where(id => _collections.All(c => c.CollectionId != id)))
        {
            _collections.Add(new ProductCollectionLink(id));
        }
    }

    /// <summary>
    /// Puts the product on sale (PRD-10). The caller has already passed the publish gate and supplies the public path for the event.
    /// Publishing a Published product changes nothing.
    /// </summary>
    public void Publish(string person, DateTimeOffset now, string path)
    {
        if (Status == ProductStatus.Published)
        {
            return;
        }

        if (Status == ProductStatus.Archived)
        {
            throw new DomainException("An archived product cannot be published.");
        }

        Status = ProductStatus.Published;
        PublishedAt = now;
        PublishedBy = person;
        ContentVersion++;
        Raise(new ProductPublishedEvent(Id, StyleId, path, ContentVersion));
    }

    /// <summary>Takes the product off sale without deleting anything (PRD-3). Only a Published product can be unpublished.</summary>
    public void Unpublish(string path)
    {
        if (Status != ProductStatus.Published)
        {
            throw new DomainException("Only a published product can be unpublished.");
        }

        Status = ProductStatus.Unpublished;
        ContentVersion++;
        Raise(new ProductUnpublishedEvent(Id, StyleId, path, ContentVersion));
    }

    /// <summary>Retires the product for good (PRD-6). Allowed from any state; archiving twice changes nothing.</summary>
    public void Archive(string path)
    {
        if (Status == ProductStatus.Archived)
        {
            return;
        }

        var wasPublished = Status == ProductStatus.Published;
        Status = ProductStatus.Archived;
        ContentVersion++; // one bump even when a second (Unpublished) event follows
        Raise(new ProductArchivedEvent(Id, StyleId, path, ContentVersion));
        if (wasPublished)
        {
            // The sale stops at the same moment; consumers that only watch "unpublished" see it too.
            Raise(new ProductUnpublishedEvent(Id, StyleId, path, ContentVersion));
        }
    }

    /// <summary>Tells the storefront a published product's data changed (a price, an image, a variant becoming sellable). Does nothing for other states.</summary>
    public bool NoteChanged(string path, string? previousPath = null)
    {
        if (Status != ProductStatus.Published)
        {
            return false;
        }

        ContentVersion++;
        Raise(new ProductChangedEvent(Id, path, previousPath, ContentVersion));
        return true;
    }

    private void EnsureEditable()
    {
        if (Status == ProductStatus.Archived)
        {
            throw new DomainException("An archived product cannot be edited.");
        }
    }
}
