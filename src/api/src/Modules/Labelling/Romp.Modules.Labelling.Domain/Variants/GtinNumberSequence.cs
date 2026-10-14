namespace Romp.Modules.Labelling.Domain.Variants;

/// <summary>
/// The GTIN counter row for one company prefix (ADR 0009). Never read or written through the entity: the allocator uses an atomic
/// SQL upsert, so this type only exists so the table is part of the model and its migration. Maps to SKU.GTIN_NO_SEQ.
/// </summary>
public sealed class GtinNumberSequence
{
    public string Prefix { get; private set; } = string.Empty;

    public long Seq { get; private set; }
}
