using Microsoft.EntityFrameworkCore;
using Romp.Modules.Warehouse.Application.Abstractions;
using Romp.Modules.Warehouse.Application.Bins;

namespace Romp.Modules.Warehouse.Infrastructure.Bins;

public sealed class BinReader(IWarehouseDbContext dbContext) : IBinReader
{
    public async Task<BinInfo?> FindByCodeAsync(string binCode, CancellationToken cancellationToken)
    {
        var code = (binCode ?? string.Empty).Trim().ToUpperInvariant();
        return await dbContext.Bins.AsNoTracking()
            .Where(b => b.BinCode == code)
            .Select(b => new BinInfo(b.Id, b.BinCode, b.IsActive))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<long, BinInfo>> GetManyAsync(IReadOnlyCollection<long> ids, CancellationToken cancellationToken) =>
        await dbContext.Bins.AsNoTracking()
            .Where(b => ids.Contains(b.Id))
            .Select(b => new BinInfo(b.Id, b.BinCode, b.IsActive))
            .ToDictionaryAsync(b => b.Id, cancellationToken);
}

public sealed class BinCountMarker(IWarehouseDbContext dbContext) : IBinCountMarker
{
    public async Task MarkCountedAsync(IReadOnlyCollection<long> binIds, DateTimeOffset countedAt, CancellationToken cancellationToken)
    {
        var bins = await dbContext.Bins.Where(b => binIds.Contains(b.Id)).ToListAsync(cancellationToken);
        foreach (var bin in bins)
        {
            bin.MarkCounted(countedAt);
        }
    }
}
