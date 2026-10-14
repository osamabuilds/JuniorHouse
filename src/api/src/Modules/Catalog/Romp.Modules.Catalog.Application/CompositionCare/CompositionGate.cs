using Microsoft.EntityFrameworkCore;
using Romp.Modules.Catalog.Application.Abstractions;
using Romp.Modules.Catalog.Domain.CompositionCare;

namespace Romp.Modules.Catalog.Application.CompositionCare;

/// <summary>What the publish gate needs to know about a product's composition and care (CMP-1, CMP-2).</summary>
public sealed record CompositionGateReport(int Rows, decimal TotalPercent, int CareInstructions);

/// <summary>The gate reader composition-care exposes; Products.Publish calls it, it never calls Products (module plan).</summary>
public interface ICompositionGate
{
    Task<CompositionGateReport> ReadAsync(long productId, CancellationToken cancellationToken);
}

public sealed class CompositionGate(ICatalogDbContext dbContext) : ICompositionGate
{
    public async Task<CompositionGateReport> ReadAsync(long productId, CancellationToken cancellationToken)
    {
        var percents = await dbContext.CompositionLines.AsNoTracking()
            .Where(l => l.ProductId == productId)
            .Select(l => l.Percent)
            .ToListAsync(cancellationToken);
        var care = await dbContext.CareInstructions.AsNoTracking().CountAsync(c => c.ProductId == productId, cancellationToken);
        return new CompositionGateReport(percents.Count, Composition.Total(percents), care);
    }
}
