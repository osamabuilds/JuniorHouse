using Romp.BuildingBlocks.Domain;
using Romp.Modules.Warehouse.Contracts;

namespace Romp.Modules.Warehouse.Domain.StockLedger;

/// <summary>
/// One requested change to stock: a variant, a grade, a bin, a signed non-zero quantity, and what caused it. The only way stock
/// changes is through <c>IStockPosting</c> with a list of these (ADR 0012).
/// </summary>
public sealed record StockPost
{
    public StockPost(long variantRefId, short gradeId, long binId, int quantityChange, short kindId, long sourceId)
    {
        if (variantRefId <= 0)
        {
            throw new DomainException("A stock change needs a variant.");
        }

        if (binId <= 0)
        {
            throw new DomainException("A stock change needs a bin.");
        }

        if (!Grades.IsKnown(gradeId))
        {
            throw new DomainException($"Unknown stock grade {gradeId}.");
        }

        if (!MovementKinds.IsKnown(kindId))
        {
            throw new DomainException($"Unknown movement kind {kindId}.");
        }

        if (quantityChange == 0)
        {
            throw new DomainException("A stock change cannot be zero.");
        }

        if (sourceId <= 0)
        {
            throw new DomainException("A stock change must point at the record that caused it.");
        }

        VariantRefId = variantRefId;
        GradeId = gradeId;
        BinId = binId;
        QuantityChange = quantityChange;
        KindId = kindId;
        SourceId = sourceId;
    }

    public long VariantRefId { get; }

    public short GradeId { get; }

    public long BinId { get; }

    public int QuantityChange { get; }

    public short KindId { get; }

    public long SourceId { get; }
}

/// <summary>
/// The ledger row (WHSE.STCK_MVMT): append-only and self-explanatory, with the balance before and after (LED-1, NFR-SC-05).
/// A trigger in the database refuses any UPDATE or DELETE.
/// </summary>
public sealed class StockMovement : Entity<long>, IAuditable
{
    private StockMovement()
    {
        ActorName = string.Empty;
    }

    public StockMovement(
        long variantRefId, short gradeId, long binId, short kindId, long sourceId, int quantityChange, int balanceBefore, int balanceAfter, string actorName)
    {
        if (balanceAfter != balanceBefore + quantityChange)
        {
            throw new DomainException("A movement must balance: after = before + change.");
        }

        if (balanceAfter < 0)
        {
            throw new DomainException("Stock in a bin can never be negative.");
        }

        VariantRefId = variantRefId;
        GradeId = gradeId;
        BinId = binId;
        KindId = kindId;
        SourceId = sourceId;
        QuantityChange = quantityChange;
        BalanceBefore = balanceBefore;
        BalanceAfter = balanceAfter;
        ActorName = actorName;
    }

    public long VariantRefId { get; private set; }

    public short GradeId { get; private set; }

    public long BinId { get; private set; }

    public short KindId { get; private set; }

    public long SourceId { get; private set; }

    public int QuantityChange { get; private set; }

    public int BalanceBefore { get; private set; }

    public int BalanceAfter { get; private set; }

    public string ActorName { get; private set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }
}

/// <summary>The current quantity of one variant in one grade in one bin (WHSE.STCK_BAL). Changed only by raw conditional SQL in the posting port.</summary>
public sealed class StockBalance : Entity<long>, IAuditable
{
    public long VariantRefId { get; private set; }

    public short GradeId { get; private set; }

    public long BinId { get; private set; }

    public int Quantity { get; private set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }
}

/// <summary>
/// Warehouse's own copy of a variant (WHSE.VRNT_REF), built from Labelling's events only (module plan). It carries what putaway,
/// the ledger and alerts need: identity, SKU, the current GTIN and whether it has been retired (D3). It also raises the stock events,
/// so they reach the outbox through the normal domain-event path in the same transaction as the stock change (ADR 0012).
/// </summary>
public sealed class VariantRef : AggregateRoot<long>, IAuditable
{
    private VariantRef()
    {
        SkuCode = string.Empty;
        Gtin = string.Empty;
    }

    public VariantRef(long variantId, long styleId, short colourId, short sizeId, string skuCode, string gtin)
    {
        VariantId = variantId;
        StyleId = styleId;
        ColourId = colourId;
        SizeId = sizeId;
        SkuCode = skuCode;
        Gtin = gtin;
    }

    /// <summary>Labelling's variant id (the id other modules know).</summary>
    public long VariantId { get; private set; }

    public long StyleId { get; private set; }

    public short ColourId { get; private set; }

    public short SizeId { get; private set; }

    public string SkuCode { get; private set; }

    public string Gtin { get; private set; }

    public bool GtinRetired { get; private set; }

    /// <summary>
    /// Goes up by one with every stock posting that touches this variant (PCT-5). Written only by the posting SQL, which takes the row
    /// lock that serialises postings of one variant; never set by the entity, so a stale tracked copy cannot overwrite it.
    /// </summary>
    public long StockVersion { get; private set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }

    /// <summary>A GTIN was assigned: it becomes the variant's current one (a replacement after a void revives the variant).</summary>
    public void ApplyAssigned(long styleId, short colourId, short sizeId, string skuCode, string gtin)
    {
        StyleId = styleId;
        ColourId = colourId;
        SizeId = sizeId;
        SkuCode = skuCode;
        Gtin = gtin;
        GtinRetired = false;
    }

    /// <summary>A GTIN was voided. Only the variant's current GTIN matters: a late void of an older number changes nothing.</summary>
    public void ApplyVoided(string gtin)
    {
        if (string.Equals(gtin, Gtin, StringComparison.Ordinal))
        {
            GtinRetired = true;
        }
    }

    /// <summary>Raises <see cref="StockLevelChangedEvent"/> with the new totals. Available equals on-hand until reservations exist (LED-5).</summary>
    public void PublishLevel(int onHandAccepted, int onHandConcession, string reasonCode, long stockVersion)
    {
        var onHand = onHandAccepted + onHandConcession;
        Raise(new StockLevelChangedEvent(
            VariantId, StyleId, ColourId, SizeId, SkuCode, Gtin, onHand, onHand, onHandAccepted, onHandConcession, reasonCode, stockVersion));
    }

    /// <summary>Raises <see cref="StockPutawayConfirmedEvent"/> (FR-SC-08): this quantity of this grade is now shelved and available.</summary>
    public void PublishPutaway(string gradeCode, int quantity, int availableAfter, long stockVersion) =>
        Raise(new StockPutawayConfirmedEvent(VariantId, StyleId, ColourId, SizeId, SkuCode, Gtin, gradeCode, quantity, availableAfter, stockVersion));
}

/// <summary>Fixed messages other code and tests can rely on (problem details never carry anything else about a refusal).</summary>
public static class StockMessages
{
    /// <summary>A change would take a bin's balance below zero (LED-3, TC-STK-05).</summary>
    public const string Insufficient = "Not enough stock in the bin for this change.";
}
