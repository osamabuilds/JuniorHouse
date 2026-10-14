using System.Text.RegularExpressions;
using Romp.BuildingBlocks.Domain;
using Romp.Modules.Labelling.Contracts;

namespace Romp.Modules.Labelling.Domain.Variants;

/// <summary>
/// A sellable variant: style × colour × size (FR-SC-06), with the readable SKU code and the GTINs it has held. At most one GTIN
/// is Active at any time (a partial unique index backs this in the database); a voided GTIN is retired for good (NFR-SC-03).
/// Maps to SKU.VRNT_MAIN. The variant must be saved (so it has an id) before a GTIN is assigned, because the events carry its id.
/// </summary>
public sealed class Variant : AggregateRoot<long>, IAuditable
{
    // SKU codes are built from lookup codes; only these characters can ever reach a tag, a file name or a log line.
    private static readonly Regex SkuPattern = new("^[A-Z0-9][A-Z0-9-]{2,119}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly List<VariantGtin> _gtins = [];

    private Variant()
    {
        SkuCode = string.Empty;
    }

    public Variant(long styleId, short colourId, short sizeId, string skuCode)
    {
        if (!SkuPattern.IsMatch(skuCode))
        {
            throw new DomainException("A SKU code may contain only capital letters, digits and hyphens (3 to 120 characters).");
        }

        StyleId = styleId;
        ColourId = colourId;
        SizeId = sizeId;
        SkuCode = skuCode;
    }

    public long StyleId { get; private set; }

    public short ColourId { get; private set; }

    public short SizeId { get; private set; }

    public string SkuCode { get; private set; }

    public IReadOnlyCollection<VariantGtin> Gtins => _gtins.AsReadOnly();

    public VariantGtin? ActiveGtin => _gtins.SingleOrDefault(g => g.StatusId == GtinStatus.Active);

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }

    /// <summary>Gives the variant its next GTIN (VAR-1). The caller supplies the item reference from the atomic counter (ADR 0009).</summary>
    public VariantGtin AssignGtin(CompanyPrefix prefix, long itemReference)
    {
        if (ActiveGtin is not null)
        {
            throw new DomainException($"Variant {SkuCode} already has an active GTIN.");
        }

        var gtin = Gtin.Create(prefix, itemReference);
        var entry = new VariantGtin(Id, gtin, prefix, itemReference);
        _gtins.Add(entry);
        Raise(new VariantGtinAssignedEvent(Id, StyleId, ColourId, SizeId, SkuCode, gtin.Value));
        return entry;
    }

    /// <summary>Retires the active GTIN at any time, with a reason (D8). The number is never issued again; units already tagged are untouched.</summary>
    public void Void(short reasonId, string? note, string person, DateTimeOffset now)
    {
        if (!VoidReasons.IsKnown(reasonId))
        {
            throw new DomainException("Choose a reason for voiding the GTIN.");
        }

        var active = ActiveGtin ?? throw new DomainException($"Variant {SkuCode} has no active GTIN to void.");
        active.Retire(reasonId, note, person, now);
        Raise(new GtinVoidedEvent(Id, StyleId, ColourId, SizeId, SkuCode, active.Gtin, VoidReasons.CodeOf(reasonId)));
    }
}
