using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.Modules.Quality.Application.Abstractions;
using Romp.Modules.Quality.Application.Batches;
using Romp.Modules.Quality.Domain.Outcomes;

namespace Romp.Modules.Quality.Application.Outcomes;

/// <summary>The rework part (with what is still outstanding) and the returned part that now awaits re-inspection.</summary>
public sealed record ReworkReturnResultDto(long BatchId, short BatchStatusId, SubBatchDto ReworkPart, SubBatchDto ReturnedPart);

/// <summary>
/// Records the vendor returning reworked goods (spec AC-26, AC-27): per size and colour, never more than was sent. The returned
/// units become a new part awaiting re-inspection (its safety checks are recorded again, AC-34); the shortfall stays visible as
/// outstanding on the rework part.
/// </summary>
public sealed record RecordReworkReturnCommand(
    long SubBatchId,
    DateOnly ReturnDate,
    string ActingPerson,
    string? Note,
    IReadOnlyList<PartLineDto> Lines) : IRequest<ReworkReturnResultDto>, IQualityCommand;

public sealed class RecordReworkReturnCommandValidator : AbstractValidator<RecordReworkReturnCommand>
{
    public RecordReworkReturnCommandValidator()
    {
        RuleFor(c => c.ActingPerson).ActingPerson();
        RuleFor(c => c.Note).MaximumLength(500).WithMessage("The note can be at most 500 characters.");
        RuleFor(c => c.Lines).NotEmpty().WithMessage("Enter the returned quantity for at least one size and colour.");
        RuleForEach(c => c.Lines).ChildRules(line =>
            line.RuleFor(l => l.Qty).GreaterThan(0).WithMessage("Each returned quantity must be a whole number above 0."));
    }
}

public sealed class RecordReworkReturnCommandHandler(IQualityDbContext dbContext, TimeProvider timeProvider)
    : IRequestHandler<RecordReworkReturnCommand, ReworkReturnResultDto>
{
    public async Task<ReworkReturnResultDto> Handle(RecordReworkReturnCommand request, CancellationToken cancellationToken)
    {
        var batch = await dbContext.Batches.WithDecisionGraph().FirstOrDefaultAsync(b => b.SubBatches.Any(s => s.Id == request.SubBatchId), cancellationToken)
            ?? throw new KeyNotFoundException($"Sub-batch {request.SubBatchId} was not found.");

        var returned = batch.RecordReworkReturn(
            request.SubBatchId, request.ReturnDate, request.ActingPerson, request.Note,
            request.Lines.Select(l => new PartLine(l.SizeId, l.ColourId, l.Qty)).ToList(),
            Abstractions.PakistanClock.Today(timeProvider));

        await dbContext.SaveChangesAsync(cancellationToken);
        var rework = batch.SubBatches.Single(s => s.Id == request.SubBatchId);
        return new ReworkReturnResultDto(batch.Id, batch.StatusId, rework.ToDto(), returned.ToDto());
    }
}
