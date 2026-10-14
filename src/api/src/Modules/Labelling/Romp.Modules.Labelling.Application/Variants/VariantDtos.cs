using Romp.Modules.Labelling.Application.Abstractions;
using Romp.Modules.Labelling.Domain;
using Romp.Modules.Labelling.Domain.Variants;

namespace Romp.Modules.Labelling.Application.Variants;

public sealed record VariantRowDto(
    long Id,
    long StyleId,
    string StyleCode,
    string StyleName,
    short ColourId,
    string ColourName,
    short SizeId,
    string SizeName,
    string SkuCode,
    string? ActiveGtin,
    int RetiredCount,
    DateTimeOffset CreatedAt);

public sealed record GtinDto(
    long Id,
    string Gtin,
    string StatusCode,
    DateTimeOffset AssignedAt,
    DateTimeOffset? RetiredAt,
    string? RetiredReasonCode,
    string? RetiredNote,
    string? RetiredBy);

public sealed record VariantDetailDto(VariantRowDto Variant, IReadOnlyList<GtinDto> Gtins);

public sealed record GtinCapacityDto(string Prefix, bool IsTestPrefix, long Capacity, long Used, long Remaining, double UsedPercent, bool Warning);

/// <summary>A variant row as read from the database, before names are added from other modules.</summary>
internal sealed record VariantRecord(
    long Id, long StyleId, short ColourId, short SizeId, string SkuCode, string? ActiveGtin, int RetiredCount, DateTimeOffset CreatedAt);

/// <summary>Adds style, colour and size names to variant rows with one lookup call per kind (no N+1, CONVENTIONS section 4).</summary>
internal static class VariantViews
{
    public static async Task<IReadOnlyList<VariantRowDto>> ToRowsAsync(
        IReadOnlyList<VariantRecord> records, ILabelStyleLookup lookup, CancellationToken cancellationToken)
    {
        if (records.Count == 0)
        {
            return [];
        }

        var styles = await lookup.GetStylesAsync(records.Select(r => r.StyleId).Distinct().ToList(), cancellationToken);
        var colours = await lookup.GetColoursAsync(records.Select(r => r.ColourId).Distinct().ToList(), cancellationToken);
        var sizes = await lookup.GetSizesAsync(records.Select(r => r.SizeId).Distinct().ToList(), cancellationToken);

        return records.Select(r => new VariantRowDto(
            r.Id,
            r.StyleId,
            styles.TryGetValue(r.StyleId, out var style) ? style.Code : "?",
            styles.TryGetValue(r.StyleId, out style) ? style.Name : "Unknown style",
            r.ColourId,
            colours.TryGetValue(r.ColourId, out var colour) ? colour.Name : "?",
            r.SizeId,
            sizes.TryGetValue(r.SizeId, out var size) ? size.Name : "?",
            r.SkuCode,
            r.ActiveGtin,
            r.RetiredCount,
            r.CreatedAt)).ToList();
    }

    public static GtinDto ToDto(VariantGtin gtin) => new(
        gtin.Id,
        gtin.Gtin,
        gtin.StatusId == GtinStatus.Active ? "Active" : "Retired",
        gtin.InsrDte,
        gtin.RetiredAt,
        gtin.RetiredReasonId is { } reason ? VoidReasons.CodeOf(reason) : null,
        gtin.RetiredNote,
        gtin.RetiredBy);
}
