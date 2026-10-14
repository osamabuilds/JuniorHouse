using Romp.BuildingBlocks.Domain;

namespace Romp.Modules.Labelling.Domain.Variants;

/// <summary>
/// One GTIN a variant has held. Rows are never deleted or reused (NFR-SC-03): a voided GTIN stays as <see cref="GtinStatus.Retired"/>
/// with the reason, person and time. Maps to SKU.VRNT_GTIN.
/// </summary>
public sealed class VariantGtin : Entity<long>, IAuditable
{
    private VariantGtin()
    {
        Gtin = string.Empty;
        Prefix = string.Empty;
    }

    internal VariantGtin(long variantId, Gtin gtin, CompanyPrefix prefix, long itemReference)
    {
        VariantId = variantId;
        Gtin = gtin.Value;
        Prefix = prefix.Value;
        ItemReference = itemReference;
        StatusId = GtinStatus.Active;
    }

    public long VariantId { get; private set; }

    public string Gtin { get; private set; }

    public string Prefix { get; private set; }

    public long ItemReference { get; private set; }

    public short StatusId { get; private set; }

    public DateTimeOffset? RetiredAt { get; private set; }

    public short? RetiredReasonId { get; private set; }

    public string? RetiredNote { get; private set; }

    public string? RetiredBy { get; private set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }

    internal void Retire(short reasonId, string? note, string person, DateTimeOffset now)
    {
        StatusId = GtinStatus.Retired;
        RetiredAt = now;
        RetiredReasonId = reasonId;
        RetiredNote = note;
        RetiredBy = person;
    }
}
