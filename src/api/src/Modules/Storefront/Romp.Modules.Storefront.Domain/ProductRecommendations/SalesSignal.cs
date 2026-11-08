namespace Romp.Modules.Storefront.Domain.ProductRecommendations;

/// <summary>Turns units sold into a 0..1 score, on a log scale so one runaway seller does not flatten the rest (REC-3).</summary>
public static class SalesSignal
{
    /// <summary><c>log(1 + units) / log(1 + max)</c> within the group; every score is 0 when nobody sold.</summary>
    public static IReadOnlyDictionary<long, double> Normalise(IReadOnlyDictionary<long, int> unitsByProduct)
    {
        ArgumentNullException.ThrowIfNull(unitsByProduct);
        var max = unitsByProduct.Values.Select(u => Math.Max(0, u)).DefaultIfEmpty(0).Max();
        var scale = max <= 0 ? 0 : Math.Log(1 + max);
        return unitsByProduct.ToDictionary(p => p.Key, p => scale <= 0 ? 0 : Math.Log(1 + Math.Max(0, p.Value)) / scale);
    }
}
