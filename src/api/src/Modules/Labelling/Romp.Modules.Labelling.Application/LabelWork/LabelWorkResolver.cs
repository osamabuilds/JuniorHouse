using Romp.Modules.Labelling.Application.Abstractions;
using Romp.Modules.Labelling.Application.Variants;
using Romp.Modules.Labelling.Domain;
using Romp.Modules.Labelling.Domain.LabelWork;

namespace Romp.Modules.Labelling.Application.LabelWork;

/// <summary>
/// Turns work items into ready-to-print items (LW-1, LW-4): find the PO's single style (D11), check it, look up the colour and
/// size codes, and have <c>variants</c> assign the GTINs (D9). Anything that cannot proceed is parked with a reason instead of
/// failing the whole message, so one bad line never blocks the rest. Runs in the caller's transaction.
/// </summary>
public sealed class LabelWorkResolver(ILabellingDbContext dbContext, ILabelStyleLookup lookup, IVariantAssigner assigner)
{
    public async Task ResolveAsync(IReadOnlyList<LabelWorkItem> items, bool confirmRetiredStyle, CancellationToken cancellationToken)
    {
        if (items.Count == 0)
        {
            return;
        }

        var styleIdByPo = new Dictionary<long, long?>();
        foreach (var poId in items.Select(i => i.PoId).Distinct())
        {
            styleIdByPo[poId] = await lookup.FindStyleIdForPoAsync(poId, cancellationToken);
        }

        var styles = await lookup.GetStylesAsync(styleIdByPo.Values.OfType<long>().Distinct().ToList(), cancellationToken);
        var colours = await lookup.GetColoursAsync(items.Select(i => i.ColourId).Distinct().ToList(), cancellationToken);
        var sizes = await lookup.GetSizesAsync(items.Select(i => i.SizeId).Distinct().ToList(), cancellationToken);

        var requests = new List<VariantRequest>();
        var ready = new List<(LabelWorkItem Item, long StyleId)>();
        foreach (var item in items)
        {
            if (styleIdByPo[item.PoId] is not { } styleId || !styles.TryGetValue(styleId, out var style))
            {
                item.Park(ParkReasons.NoStyle, null);
                continue;
            }

            if (!style.IsActive && !confirmRetiredStyle)
            {
                item.Park(ParkReasons.StyleRetired, style.Id);
                continue;
            }

            if (!colours.TryGetValue(item.ColourId, out var colour) || !sizes.TryGetValue(item.SizeId, out var size))
            {
                item.Park(ParkReasons.UnknownCode, style.Id);
                continue;
            }

            requests.Add(new VariantRequest(style.Id, style.Code, colour.Id, colour.Code, size.Id, size.Code));
            ready.Add((item, style.Id));
        }

        if (requests.Count > 0)
        {
            var variants = await assigner.EnsureGtinsAsync(requests, cancellationToken);
            foreach (var (item, styleId) in ready)
            {
                item.MarkReady(styleId, variants[(styleId, item.ColourId, item.SizeId)]);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
