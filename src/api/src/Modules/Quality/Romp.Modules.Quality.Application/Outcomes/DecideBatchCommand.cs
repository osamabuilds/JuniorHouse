using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.Modules.Quality.Application.Abstractions;
using Romp.Modules.Quality.Application.Batches;
using Romp.Modules.Quality.Domain.Batches;
using Romp.Modules.Quality.Domain.Outcomes;

namespace Romp.Modules.Quality.Application.Outcomes;

public sealed record PartLineDto(short SizeId, short ColourId, int Qty);

/// <summary>One part of a batch decision. <c>Lines</c> may be left out only when the whole batch gets one outcome.</summary>
public sealed record DecisionPartDto(short OutcomeId, string? Reason, IReadOnlyList<PartLineDto>? Lines);

public sealed record BatchDecisionDto(long BatchId, short BatchStatusId, IReadOnlyList<SubBatchDto> SubBatches);

/// <summary>Loads the graph a decision needs: the batch's lines, every part with its lines and returns, and each inspection with its safety results.</summary>
internal static class BatchDecisionGraph
{
    public static IQueryable<QcBatch> WithDecisionGraph(this IQueryable<QcBatch> query) => query
        .Include(b => b.Lines)
        .Include(b => b.SubBatches).ThenInclude(s => s.Lines)
        .Include(b => b.SubBatches).ThenInclude(s => s.Returns)
        .Include(b => b.Inspections).ThenInclude(i => i.SafetyChecks).ThenInclude(s => s.AffectedLines);
}

/// <summary>
/// Records the first decision on a batch, whole or split (spec AC-19..AC-25, AC-28, AC-29, AC-31, AC-32). Each part is exactly one
/// of Accept, Accept with concession, Reject or Rework; the parts add up to the counted quantity; an accepted part is released at
/// once. The <c>QC.BatchAccepted</c> / <c>QC.BatchRejected</c> / rework-requested events are written in the same transaction.
/// </summary>
public sealed record DecideBatchCommand(long BatchId, string ActingPerson, IReadOnlyList<DecisionPartDto> Parts)
    : IRequest<BatchDecisionDto>, IQualityCommand;

public sealed class DecideBatchCommandValidator : AbstractValidator<DecideBatchCommand>
{
    public DecideBatchCommandValidator()
    {
        RuleFor(c => c.ActingPerson).ActingPerson();
        RuleFor(c => c.Parts).NotEmpty().WithMessage("Decide at least one part of the batch.");
        RuleForEach(c => c.Parts).ChildRules(part =>
            part.RuleFor(p => p.Reason).MaximumLength(500).WithMessage("The reason can be at most 500 characters."));
    }
}

public sealed class DecideBatchCommandHandler(IQualityDbContext dbContext, TimeProvider timeProvider)
    : IRequestHandler<DecideBatchCommand, BatchDecisionDto>
{
    public async Task<BatchDecisionDto> Handle(DecideBatchCommand request, CancellationToken cancellationToken)
    {
        var batch = await dbContext.Batches.WithDecisionGraph().FirstOrDefaultAsync(b => b.Id == request.BatchId, cancellationToken)
            ?? throw new KeyNotFoundException($"QC batch {request.BatchId} was not found.");

        // One part with no lines means "the whole batch".
        var parts = request.Parts.Select(p => new DecisionPart(
            p.OutcomeId, p.Reason,
            p.Lines is { Count: > 0 } || request.Parts.Count > 1
                ? (p.Lines ?? []).Select(l => new PartLine(l.SizeId, l.ColourId, l.Qty)).ToList()
                : batch.Lines.Select(l => new PartLine(l.SizeId, l.ColourId, l.Qty)).ToList())).ToList();

        var created = batch.Decide(parts, request.ActingPerson, timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken); // the parts have ids now, so their events can carry them
        foreach (var sub in created)
        {
            batch.RaiseOutcomeEvent(sub);
        }

        return new BatchDecisionDto(batch.Id, batch.StatusId, created.Select(s => s.ToDto()).ToList());
    }
}

/// <summary>Decides the returned quantity of a rework part after its re-inspection (spec AC-26, AC-34): one outcome, same rules.</summary>
public sealed record DecideSubBatchCommand(long SubBatchId, short OutcomeId, string? Reason, string ActingPerson)
    : IRequest<BatchDecisionDto>, IQualityCommand;

public sealed class DecideSubBatchCommandValidator : AbstractValidator<DecideSubBatchCommand>
{
    public DecideSubBatchCommandValidator()
    {
        RuleFor(c => c.ActingPerson).ActingPerson();
        RuleFor(c => c.Reason).MaximumLength(500).WithMessage("The reason can be at most 500 characters.");
    }
}

public sealed class DecideSubBatchCommandHandler(IQualityDbContext dbContext, TimeProvider timeProvider)
    : IRequestHandler<DecideSubBatchCommand, BatchDecisionDto>
{
    public async Task<BatchDecisionDto> Handle(DecideSubBatchCommand request, CancellationToken cancellationToken)
    {
        var batch = await dbContext.Batches.WithDecisionGraph().FirstOrDefaultAsync(b => b.SubBatches.Any(s => s.Id == request.SubBatchId), cancellationToken)
            ?? throw new KeyNotFoundException($"Sub-batch {request.SubBatchId} was not found.");

        var sub = batch.DecideSubBatch(request.SubBatchId, request.OutcomeId, request.Reason, request.ActingPerson, timeProvider.GetUtcNow());
        batch.RaiseOutcomeEvent(sub);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new BatchDecisionDto(batch.Id, batch.StatusId, [sub.ToDto()]);
    }
}
