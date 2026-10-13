using Romp.Modules.Catalog.Contracts;
using Romp.Modules.Labelling.Application.Abstractions;
using Romp.Modules.Reference.Contracts;
using Romp.Modules.Vendor.Contracts;

namespace Romp.Modules.Labelling.Infrastructure.LabelWork;

/// <summary>
/// Adapts other modules' <c>*.Contracts</c> to Labelling's one lookup port (ADR 0002, ADR 0011). This is the only place Labelling
/// touches Vendor, Catalog or Reference, and it never touches their tables.
/// </summary>
public sealed class ContractsLabelStyleLookup(
    IPurchaseOrderQueries purchaseOrders, IStyleQueries styles, IApparelCodeQueries codes, TimeProvider timeProvider)
    : ILabelStyleLookup
{
    public async Task<long?> FindStyleIdForPoAsync(long poId, CancellationToken cancellationToken)
    {
        // One style per PO (D11), identical in every revision, so the terms in force today name it.
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().ToOffset(TimeSpan.FromHours(5)).DateTime);
        var terms = await purchaseOrders.GetTermsAtAsync(poId, today, cancellationToken);
        return terms?.StyleId;
    }

    public async Task<IReadOnlyDictionary<long, LabelStyle>> GetStylesAsync(IReadOnlyCollection<long> styleIds, CancellationToken cancellationToken)
    {
        var found = await styles.GetLabelInfoAsync(styleIds, cancellationToken);
        return found.ToDictionary(
            pair => pair.Key,
            pair => new LabelStyle(pair.Value.Id, pair.Value.Code, pair.Value.Name, pair.Value.TargetRetailPrice, pair.Value.TargetPriceChangedAt, pair.Value.IsActive));
    }

    public async Task<IReadOnlyDictionary<short, LabelCode>> GetColoursAsync(IReadOnlyCollection<short> ids, CancellationToken cancellationToken) =>
        Map(await codes.GetColoursAsync(ids, cancellationToken));

    public async Task<IReadOnlyDictionary<short, LabelCode>> GetSizesAsync(IReadOnlyCollection<short> ids, CancellationToken cancellationToken) =>
        Map(await codes.GetSizesAsync(ids, cancellationToken));

    private static Dictionary<short, LabelCode> Map(IReadOnlyDictionary<short, ApparelCode> source) =>
        source.ToDictionary(pair => pair.Key, pair => new LabelCode(pair.Value.Id, pair.Value.Code, pair.Value.Name));
}
