using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Domain;
using Romp.Modules.Warehouse.Application.Abstractions;
using Romp.Modules.Warehouse.Application.Adjustments;
using Romp.Modules.Warehouse.Application.Bins;
using Romp.Modules.Warehouse.Application.StockLedger;
using Romp.Modules.Warehouse.Domain;
using Romp.Modules.Warehouse.Domain.CycleCounts;
using Romp.Modules.Warehouse.Domain.Putaway;

namespace Romp.Modules.Warehouse.Application.CycleCounts;

/// <summary>Allocates <c>CC-{YYYY}-{NNNN}</c> with an atomic counter (ADR 0006). Implemented in Infrastructure over SQL.</summary>
public interface ICountNumberAllocator
{
    Task<string> AllocateAsync(CancellationToken cancellationToken);
}

public sealed record CountLineInput(string Gtin, short GradeId, int Quantity);

public sealed record CountReasonInput(long BinId, long VariantRefId, short GradeId, short ReasonId, bool Reconfirmed);

internal static class CountSupport
{
    public static async Task<Dictionary<long, long>> CurrentMarksAsync(IWarehouseDbContext dbContext, IReadOnlyCollection<long> binIds, CancellationToken cancellationToken) =>
        await dbContext.Movements.AsNoTracking()
            .Where(m => binIds.Contains(m.BinId))
            .GroupBy(m => m.BinId)
            .Select(g => new { BinId = g.Key, Mark = g.Max(m => m.Id) })
            .ToDictionaryAsync(x => x.BinId, x => x.Mark, cancellationToken);

    /// <summary>A repeatable id for the adjustment a count raises for one line, so confirming twice can never adjust twice (CC-5).</summary>
    public static Guid AdjustmentKey(long countId, long binId, long variantRefId, short gradeId) =>
        new(SHA256.HashData(Encoding.UTF8.GetBytes($"cycle-count:{countId}:{binId}:{variantRefId}:{gradeId}"))[..16]);
}

/// <summary>Starts a count of the chosen bins (CC-1). A bin already in an open count is refused (CC-3).</summary>
public sealed record CreateCycleCountCommand(IReadOnlyList<long> BinIds, string ActingPerson) : IRequest<CycleCountDto>, IWarehouseCommand;

public sealed class CreateCycleCountCommandValidator : AbstractValidator<CreateCycleCountCommand>
{
    public CreateCycleCountCommandValidator()
    {
        RuleFor(c => c.BinIds).NotEmpty().Must(ids => ids.Count <= 200).WithMessage("A count covers at most 200 bins.");
        RuleForEach(c => c.BinIds).GreaterThan(0);
        RuleFor(c => c.ActingPerson).ActingPerson();
    }
}

public sealed class CreateCycleCountCommandHandler(IWarehouseDbContext dbContext, IBinReader bins, ICountNumberAllocator numbers)
    : IRequestHandler<CreateCycleCountCommand, CycleCountDto>
{
    public async Task<CycleCountDto> Handle(CreateCycleCountCommand request, CancellationToken cancellationToken)
    {
        var ids = request.BinIds.Distinct().ToList();
        var found = await bins.GetManyAsync(ids, cancellationToken);
        var missing = ids.Where(i => !found.ContainsKey(i)).ToList();
        if (missing.Count > 0)
        {
            throw new DomainException($"Bin(s) {string.Join(", ", missing)} do not exist.");
        }

        if (found.Values.FirstOrDefault(b => !b.IsActive) is { } inactive)
        {
            throw new DomainException($"Bin {inactive.BinCode} is inactive and cannot be counted.");
        }

        var busy = await dbContext.CountBins.AsNoTracking().Where(b => b.IsOpen && ids.Contains(b.BinId)).Select(b => b.BinId).ToListAsync(cancellationToken);
        if (busy.Count > 0)
        {
            throw new DomainException($"Bin {found[busy[0]].BinCode} is already part of an open count.");
        }

        var marks = await CountSupport.CurrentMarksAsync(dbContext, ids, cancellationToken);
        var count = new CycleCount(
            await numbers.AllocateAsync(cancellationToken),
            ids.ToDictionary(i => i, i => marks.GetValueOrDefault(i)),
            request.ActingPerson.Trim());
        dbContext.CycleCounts.Add(count);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await CycleCountViews.BuildAsync(dbContext, count, cancellationToken);
    }
}

/// <summary>
/// Records what was counted in one bin (CC-2, CC-6, CC-10). The response never carries a system quantity (a blind count). An empty list
/// means "counted, found nothing". With <c>Recount</c> the bin's staleness mark moves to now, which is how a changed bin is recounted (CC-7).
/// </summary>
public sealed record SubmitCountLinesCommand(long CountId, string BinCode, IReadOnlyList<CountLineInput> Lines, bool Recount, string ActingPerson)
    : IRequest<CycleCountDto>, IWarehouseCommand;

public sealed class SubmitCountLinesCommandValidator : AbstractValidator<SubmitCountLinesCommand>
{
    public SubmitCountLinesCommandValidator()
    {
        RuleFor(c => c.CountId).GreaterThan(0);
        RuleFor(c => c.BinCode).NotEmpty().MaximumLength(12);
        RuleFor(c => c.Lines).NotNull().Must(l => l is null || l.Count <= 2_000).WithMessage("At most 2,000 lines per bin.");
        RuleForEach(c => c.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.Gtin).Must(g => Gtin13.IsValid(g)).WithMessage("A counted barcode is not a valid GTIN-13.");
            line.RuleFor(l => l.GradeId).Must(Grades.IsKnown).WithMessage("Choose a stock grade.");
            line.RuleFor(l => l.Quantity).InclusiveBetween(0, 100_000).WithMessage("Counted quantities are whole numbers from 0 to 100,000.");
        });
        RuleFor(c => c.ActingPerson).ActingPerson();
    }
}

public sealed class SubmitCountLinesCommandHandler(IWarehouseDbContext dbContext, IBinReader bins, IStockVariantReader variants)
    : IRequestHandler<SubmitCountLinesCommand, CycleCountDto>
{
    public async Task<CycleCountDto> Handle(SubmitCountLinesCommand request, CancellationToken cancellationToken)
    {
        var count = await dbContext.CycleCounts.Include(c => c.Bins).Include(c => c.Lines)
            .FirstOrDefaultAsync(c => c.Id == request.CountId, cancellationToken)
            ?? throw new KeyNotFoundException($"Count {request.CountId} was not found.");
        var bin = await bins.FindByCodeAsync(request.BinCode, cancellationToken)
            ?? throw new DomainException($"Bin {request.BinCode.Trim().ToUpperInvariant()} does not exist.");

        var resolved = new List<(long VariantRefId, short GradeId, int Quantity)>();
        foreach (var line in request.Lines)
        {
            var variant = await variants.FindByGtinAsync(line.Gtin, cancellationToken)
                ?? throw new DomainException($"Barcode {line.Gtin} is not a known variant.");
            if (variant.GtinRetired && line.Quantity > 0)
            {
                throw new DomainException($"Barcode {line.Gtin} has been retired and cannot be counted as present.");
            }

            resolved.Add((variant.Id, line.GradeId, line.Quantity));
        }

        var marks = await CountSupport.CurrentMarksAsync(dbContext, [bin.Id], cancellationToken);
        count.SetBinCounts(bin.Id, resolved, request.Recount, marks.GetValueOrDefault(bin.Id));
        await dbContext.SaveChangesAsync(cancellationToken);

        return await CycleCountViews.BuildAsync(dbContext, count, cancellationToken);
    }
}

/// <summary>Cancels an open count: no stock changes and the bins are free again (CC-9).</summary>
public sealed record CancelCycleCountCommand(long CountId, string ActingPerson) : IRequest<CycleCountDto>, IWarehouseCommand;

public sealed class CancelCycleCountCommandHandler(IWarehouseDbContext dbContext) : IRequestHandler<CancelCycleCountCommand, CycleCountDto>
{
    public async Task<CycleCountDto> Handle(CancelCycleCountCommand request, CancellationToken cancellationToken)
    {
        var count = await dbContext.CycleCounts.Include(c => c.Bins).Include(c => c.Lines)
            .FirstOrDefaultAsync(c => c.Id == request.CountId, cancellationToken)
            ?? throw new KeyNotFoundException($"Count {request.CountId} was not found.");
        count.Cancel();
        await dbContext.SaveChangesAsync(cancellationToken);
        return await CycleCountViews.BuildAsync(dbContext, count, cancellationToken);
    }
}

/// <summary>
/// Confirms a reviewed count (CC-4, CC-5, CC-7, CC-8, CC-12): every bin submitted, none changed since the count opened, a reason on every
/// variance. Each variance becomes an ordinary adjustment (same rules, same transaction), zero-variance lines raise nothing, the bins'
/// last-counted time moves, and the count closes. Confirming again changes nothing (deterministic adjustment keys, closed status).
/// </summary>
public sealed record ConfirmCycleCountCommand(long CountId, IReadOnlyList<CountReasonInput> Reasons, string ActingPerson)
    : IRequest<CycleCountDto>, IWarehouseCommand;

public sealed class ConfirmCycleCountCommandValidator : AbstractValidator<ConfirmCycleCountCommand>
{
    public ConfirmCycleCountCommandValidator()
    {
        RuleFor(c => c.CountId).GreaterThan(0);
        RuleFor(c => c.Reasons).NotNull();
        RuleFor(c => c.ActingPerson).ActingPerson();
    }
}

public sealed class ConfirmCycleCountCommandHandler(
    IWarehouseDbContext dbContext, IAdjustmentPoster poster, IBinCountMarker marker, TimeProvider timeProvider)
    : IRequestHandler<ConfirmCycleCountCommand, CycleCountDto>
{
    public async Task<CycleCountDto> Handle(ConfirmCycleCountCommand request, CancellationToken cancellationToken)
    {
        var count = await dbContext.CycleCounts.Include(c => c.Bins).Include(c => c.Lines)
            .FirstOrDefaultAsync(c => c.Id == request.CountId, cancellationToken)
            ?? throw new KeyNotFoundException($"Count {request.CountId} was not found.");
        if (!count.IsOpen)
        {
            return await CycleCountViews.BuildAsync(dbContext, count, cancellationToken); // already closed or cancelled: nothing to do
        }

        var review = await CycleCountViews.ReviewAsync(dbContext, count, cancellationToken);
        if (!review.AllBinsSubmitted)
        {
            throw new DomainException("Every bin must be counted before the count can be confirmed.");
        }

        if (review.Lines.Where(l => l.Stale).Select(l => l.BinCode).Distinct().ToList() is { Count: > 0 } stale)
        {
            throw new DomainException($"Stock moved in bin(s) {string.Join(", ", stale)} after the count opened. Recount them before confirming.");
        }

        var actor = request.ActingPerson.Trim();
        foreach (var line in review.Lines)
        {
            var domainLine = count.EnsureLine(line.BinId, line.VariantRefId, line.GradeId);
            if (line.Variance == 0)
            {
                domainLine.Resolve(line.SystemQuantity, null, null);
                continue;
            }

            var reason = request.Reasons.FirstOrDefault(r => r.BinId == line.BinId && r.VariantRefId == line.VariantRefId && r.GradeId == line.GradeId)
                ?? throw new DomainException($"Choose a reason for the variance on {line.SkuCode} in bin {line.BinCode}.");

            var adjustment = await poster.PostAsync(
                new AdjustmentRequest(
                    line.VariantRefId, null, line.BinCode, line.GradeId, line.Variance, reason.ReasonId, $"Cycle count {count.CountNo}",
                    reason.Reconfirmed, CountSupport.AdjustmentKey(count.Id, line.BinId, line.VariantRefId, line.GradeId), null, actor),
                cancellationToken);
            domainLine.Resolve(line.SystemQuantity, reason.ReasonId, adjustment.Id);
        }

        count.Close();
        await marker.MarkCountedAsync([.. count.Bins.Select(b => b.BinId)], timeProvider.GetUtcNow(), cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await CycleCountViews.BuildAsync(dbContext, count, cancellationToken);
    }
}
