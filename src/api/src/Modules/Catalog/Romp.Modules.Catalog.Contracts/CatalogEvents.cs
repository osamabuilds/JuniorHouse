using Romp.BuildingBlocks.Domain;

namespace Romp.Modules.Catalog.Contracts;

/// <summary>
/// The shared envelope of every Catalog event (ADR 0004). Payloads carry ids and the public path only: no prices, notes, staff names or
/// personal data, so a consumer reads the details through <see cref="IProductPathQueries"/> or its own read model. Schema changes
/// within a version are additive (ADR 0011). The product events carry the product's content version (0 when the sender predates it).
/// </summary>
public abstract record CatalogEvent : IOutboxEvent
{
    public Guid EventId { get; } = Guid.NewGuid();

    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;

    public string EventType => GetType().Name;

    public abstract string AggregateType { get; }

    public abstract long AggregateId { get; }

    public abstract short SchemaVersion { get; }
}

/// <summary>A product went on sale at <c>Path</c> (PRD-10).</summary>
public sealed record ProductPublishedEvent(long ProductId, long StyleId, string Path, long Version = 0) : CatalogEvent
{
    public override string AggregateType => "Product";

    public override long AggregateId => ProductId;

    public override short SchemaVersion => 1;
}

/// <summary>A product was taken off sale; its path now answers "gone" only when archived (PRD-3, PRD-10).</summary>
public sealed record ProductUnpublishedEvent(long ProductId, long StyleId, string Path, long Version = 0) : CatalogEvent
{
    public override string AggregateType => "Product";

    public override long AggregateId => ProductId;

    public override short SchemaVersion => 1;
}

/// <summary>A product was archived for good (PRD-6); its path answers "gone".</summary>
public sealed record ProductArchivedEvent(long ProductId, long StyleId, string Path, long Version = 0) : CatalogEvent
{
    public override string AggregateType => "Product";

    public override long AggregateId => ProductId;

    public override short SchemaVersion => 1;
}

/// <summary>
/// A published product's storefront data changed (price, content, images, composition, path, a variant becoming sellable). When the
/// path moved, <c>PreviousPath</c> is the old one so a consumer can invalidate both.
/// </summary>
public sealed record ProductChangedEvent(long ProductId, string Path, string? PreviousPath, long Version = 0) : CatalogEvent
{
    public override string AggregateType => "Product";

    public override long AggregateId => ProductId;

    public override short SchemaVersion => 1;
}

/// <summary>A size chart was created or edited; products of its category and gender show the new chart.</summary>
public sealed record SizeChartChangedEvent(long ChartId, short CategoryId, short GenderId) : CatalogEvent
{
    public override string AggregateType => "SizeChart";

    public override long AggregateId => ChartId;

    public override short SchemaVersion => 1;
}
