using System.Text;
using Romp.BuildingBlocks.Domain;
using Romp.Modules.Labelling.Application.Abstractions;
using Romp.Modules.Labelling.Application.Variants;
using Romp.Modules.Labelling.Domain;
using Romp.Modules.Labelling.Domain.Tags;

namespace Romp.Modules.Labelling.Application.Tags;

/// <summary>What to print: which GTIN row, how many tags, the QC outcome (for the "2ND" mark) and the price at print time (D4).</summary>
public sealed record TagRequest(long VariantGtinId, int Qty, short OutcomeId, decimal Price);

/// <summary>A rendered tag document ready for a printer, plus the SKU and GTIN it carries (for the job's event).</summary>
public sealed record LabelDocument(short FormatId, byte[] Content, string ContentType, string SkuCode, string Gtin);

/// <summary>
/// The public surface of <c>tags</c> for other features. Pure rendering over data it reads through ports: it knows nothing about
/// printers, files or queues (TAG-9).
/// </summary>
public interface ITagDocumentBuilder
{
    /// <summary>Builds the tag for a GTIN row in the format a printer needs. Refuses a retired GTIN (PJ-8) and a corrupt one (TAG-2).</summary>
    Task<LabelDocument> BuildAsync(TagRequest request, short formatId, CancellationToken cancellationToken);
}

public sealed class TagDocumentBuilder(IVariantReader variants, ILabelStyleLookup lookup) : ITagDocumentBuilder
{
    public async Task<LabelDocument> BuildAsync(TagRequest request, short formatId, CancellationToken cancellationToken)
    {
        var snapshot = await variants.GetByGtinIdAsync(request.VariantGtinId, cancellationToken)
            ?? throw new DomainException("That GTIN no longer exists.");
        if (snapshot.ActiveGtinId != request.VariantGtinId || snapshot.ActiveGtin is null)
        {
            throw new DomainException($"The GTIN for {snapshot.SkuCode} has been voided, so its tag cannot be printed.");
        }

        var styles = await lookup.GetStylesAsync([snapshot.StyleId], cancellationToken);
        var colours = await lookup.GetColoursAsync([snapshot.ColourId], cancellationToken);
        var sizes = await lookup.GetSizesAsync([snapshot.SizeId], cancellationToken);

        var tag = new TagData(
            snapshot.SkuCode,
            snapshot.ActiveGtin,
            styles.TryGetValue(snapshot.StyleId, out var style) ? style.Name : snapshot.SkuCode,
            colours.TryGetValue(snapshot.ColourId, out var colour) ? colour.Name : string.Empty,
            sizes.TryGetValue(snapshot.SizeId, out var size) ? size.Name : string.Empty,
            request.Price,
            request.OutcomeId == LabelOutcome.Concession);

        return formatId == PrintFormat.Zpl
            ? new LabelDocument(formatId, Encoding.UTF8.GetBytes(TagZpl.Render(tag, request.Qty)), "application/x-zpl", snapshot.SkuCode, snapshot.ActiveGtin)
            : new LabelDocument(formatId, TagPdf.Render(tag), "application/pdf", snapshot.SkuCode, snapshot.ActiveGtin);
    }
}
