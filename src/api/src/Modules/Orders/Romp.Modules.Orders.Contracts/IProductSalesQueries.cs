namespace Romp.Modules.Orders.Contracts;

/// <summary>
/// What Storefront may read about sales (product recommendations REC-4, ADR 0029): units per product per UTC day over a range, counting
/// only orders that are not cancelled. Totals only: no order id, customer, address or child data crosses this contract (NFR-27..30).
/// </summary>
public interface IProductSalesQueries
{
    /// <summary>Units sold per product per UTC day from <paramref name="fromDay"/> to <paramref name="toDay"/>, both inclusive. Days with no sales are absent.</summary>
    Task<IReadOnlyList<ProductDayUnits>> UnitsSoldByProductAndDayAsync(DateOnly fromDay, DateOnly toDay, CancellationToken cancellationToken);
}

public sealed record ProductDayUnits(long ProductId, DateOnly Day, int Units);
