using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Domain;
using Romp.Modules.Orders.Contracts;
using Romp.Modules.Returns.Application.Abstractions;
using Romp.Modules.Returns.Domain;
using Romp.Modules.Returns.Domain.ReverseQc;
using Romp.Modules.Warehouse.Contracts;

namespace Romp.Modules.Returns.Application.ReverseQc;

/// <summary>
/// Opens the case for a parcel returned to origin, reading the order lines once (RQC-1, RQC-13). The same order again opens nothing more; an unknown
/// order opens nothing. Returns the case id, or null when nothing was opened.
/// </summary>
public sealed record OpenReturnCaseCommand(long OrderId, string OrderNumber) : IRequest<long?>, IReturnsCommand;

public sealed class OpenReturnCaseCommandHandler(IReturnsDbContext dbContext, IOrderFulfilmentQueries orders) : IRequestHandler<OpenReturnCaseCommand, long?>
{
    public async Task<long?> Handle(OpenReturnCaseCommand request, CancellationToken cancellationToken)
    {
        var exists = await dbContext.Cases.AsNoTracking().AnyAsync(c => c.OrderId == request.OrderId && c.KindId == ReturnKinds.Rto, cancellationToken);
        if (exists)
        {
            return null;
        }

        var order = await orders.FindAsync(request.OrderId, cancellationToken);
        if (order is null || order.Lines.Count == 0)
        {
            return null;
        }

        var created = ReturnCase.Open(
            request.OrderId,
            request.OrderNumber,
            ReturnKinds.Rto,
            [.. order.Lines.Select(l => new ReturnLineSeed(l.VariantId, l.Sku, l.Name, l.Colour, l.Size, l.Quantity))]);
        dbContext.Cases.Add(created);
        await dbContext.SaveChangesAsync(cancellationToken);
        return created.Id;
    }
}

/// <summary>
/// Staff receive the parcel with the seal state and the bin (RQC-2, RQC-3). An intact seal restocks every line as sellable stock and completes the
/// case; otherwise it waits for inspection. Warehouse is asked first, with a key per case line, so a repeat or a crash never restocks twice, and a
/// refusal (an unknown bin) leaves the case as it was (RQC-10).
/// </summary>
public sealed record ReceiveReturnCaseCommand(long CaseId, bool SealIntact, string? BinCode, string Staff) : IRequest<ReturnCaseDetailDto?>, IReturnsCommand;

public sealed class ReceiveReturnCaseCommandHandler(IReturnsDbContext dbContext, IStockReturns stock, TimeProvider clock) : IRequestHandler<ReceiveReturnCaseCommand, ReturnCaseDetailDto?>
{
    public async Task<ReturnCaseDetailDto?> Handle(ReceiveReturnCaseCommand request, CancellationToken cancellationToken)
    {
        var found = await dbContext.Cases.Include(c => c.Lines).FirstOrDefaultAsync(c => c.Id == request.CaseId, cancellationToken);
        if (found is null)
        {
            return null;
        }

        var now = clock.GetUtcNow();
        found.Receive(request.SealIntact, request.BinCode ?? string.Empty, request.Staff, now);
        if (request.SealIntact)
        {
            foreach (var line in found.Lines)
            {
                await RestockAsync.RunAsync(stock, found, line, ReturnDecisions.Restock, line.OrderedQuantity, request.Staff, cancellationToken);
                found.DecideLine(line.Id, ReturnDecisions.Restock, line.OrderedQuantity, null, request.Staff, now);
            }

            found.Complete(request.Staff, now);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return ReturnCaseMapper.ToDetail(found);
    }
}

/// <summary>Staff decide one line of a case in inspection (RQC-4 to RQC-6, RQC-9, RQC-10).</summary>
public sealed record DecideReturnLineCommand(long CaseId, long LineId, string? DecisionCode, int Quantity, string? Note, string Staff) : IRequest<ReturnCaseDetailDto?>, IReturnsCommand;

public sealed class DecideReturnLineCommandHandler(IReturnsDbContext dbContext, IStockReturns stock, TimeProvider clock) : IRequestHandler<DecideReturnLineCommand, ReturnCaseDetailDto?>
{
    public async Task<ReturnCaseDetailDto?> Handle(DecideReturnLineCommand request, CancellationToken cancellationToken)
    {
        var found = await dbContext.Cases.Include(c => c.Lines).FirstOrDefaultAsync(c => c.Id == request.CaseId, cancellationToken);
        if (found is null)
        {
            return null;
        }

        var decision = ReturnDecisions.FromCode(request.DecisionCode) ?? throw new DomainException("Choose Restock, Restock as seconds or Reject.");

        // The rules run on the case first, so a bad quantity, a repeat or a missing note is refused before any stock moves.
        found.DecideLine(request.LineId, decision, request.Quantity, request.Note, request.Staff, clock.GetUtcNow());
        if (decision != ReturnDecisions.Reject)
        {
            var line = found.Lines.First(l => l.Id == request.LineId);
            await RestockAsync.RunAsync(stock, found, line, decision, request.Quantity, request.Staff, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return ReturnCaseMapper.ToDetail(found);
    }
}

/// <summary>Staff complete a case whose lines are all decided; the result is announced once (RQC-7, RQC-8).</summary>
public sealed record CompleteReturnCaseCommand(long CaseId, string Staff) : IRequest<ReturnCaseDetailDto?>, IReturnsCommand;

public sealed class CompleteReturnCaseCommandHandler(IReturnsDbContext dbContext, TimeProvider clock) : IRequestHandler<CompleteReturnCaseCommand, ReturnCaseDetailDto?>
{
    public async Task<ReturnCaseDetailDto?> Handle(CompleteReturnCaseCommand request, CancellationToken cancellationToken)
    {
        var found = await dbContext.Cases.Include(c => c.Lines).FirstOrDefaultAsync(c => c.Id == request.CaseId, cancellationToken);
        if (found is null)
        {
            return null;
        }

        found.Complete(request.Staff, clock.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);
        return ReturnCaseMapper.ToDetail(found);
    }
}

/// <summary>One call to Warehouse per line, keyed by case and line, so the same line never adds stock twice (RQC-9).</summary>
internal static class RestockAsync
{
    public static async Task RunAsync(IStockReturns stock, ReturnCase returnCase, ReturnCaseLine line, short decision, int quantity, string staff, CancellationToken cancellationToken)
    {
        var result = await stock.RestockAsync(
            new RestockRequest(line.VariantId, returnCase.ReceivingBinCode!, quantity, decision == ReturnDecisions.Seconds, $"return:{returnCase.Id}:line:{line.Id}", staff),
            cancellationToken);
        if (!result.Restocked)
        {
            throw new DomainException(result.Refusal ?? "The stock could not be put back.");
        }
    }
}
