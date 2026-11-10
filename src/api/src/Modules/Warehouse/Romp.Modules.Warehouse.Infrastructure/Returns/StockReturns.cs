using MediatR;
using Romp.Modules.Warehouse.Application.Returns;
using Romp.Modules.Warehouse.Contracts;

namespace Romp.Modules.Warehouse.Infrastructure.Returns;

/// <summary>The Warehouse side of <see cref="IStockReturns"/>: one command, so one Warehouse transaction (ADR 0033).</summary>
public sealed class StockReturns(ISender sender) : IStockReturns
{
    public Task<RestockResult> RestockAsync(RestockRequest request, CancellationToken cancellationToken) =>
        sender.Send(new RestockCommand(request), cancellationToken);
}
