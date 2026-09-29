using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.Modules.Quality.Application.Abstractions;
using Romp.Modules.Quality.Application.Batches;
using Romp.Modules.Quality.Domain.SafetyChecks;

namespace Romp.Modules.Quality.Application.SafetyChecks;

/// <summary>
/// Records both mandatory child-safety checks of an inspection round (BRD 5.11, spec AC-30..AC-34): choking hazard on small
/// trims and drawstring safety on necklines. Each is Pass, Fail or Not applicable (a reason is mandatory for Not applicable);
/// a failed check may list the size and colour lines it applies to (none listed means the whole batch). Recording again before
/// any decision corrects the entry.
/// </summary>
public sealed record RecordSafetyChecksCommand(
    long InspectionId,
    string ActingPerson,
    IReadOnlyList<SafetyCheckDto> Checks) : IRequest<InspectionDto>, IQualityCommand;

public sealed class RecordSafetyChecksCommandValidator : AbstractValidator<RecordSafetyChecksCommand>
{
    public RecordSafetyChecksCommandValidator()
    {
        RuleFor(c => c.ActingPerson).ActingPerson();
        RuleFor(c => c.Checks).NotNull().NotEmpty().WithMessage("Record a result for both child-safety checks.");
        RuleForEach(c => c.Checks).ChildRules(check =>
            check.RuleFor(s => s.Reason).MaximumLength(500).WithMessage("The reason can be at most 500 characters."));
    }
}

public sealed class RecordSafetyChecksCommandHandler(IQualityDbContext dbContext) : IRequestHandler<RecordSafetyChecksCommand, InspectionDto>
{
    public async Task<InspectionDto> Handle(RecordSafetyChecksCommand request, CancellationToken cancellationToken)
    {
        var batch = await dbContext.Batches
            .Include(b => b.Lines)
            .Include(b => b.SubBatches).ThenInclude(s => s.Lines)
            .Include(b => b.Inspections).ThenInclude(i => i.SafetyChecks).ThenInclude(s => s.AffectedLines)
            .FirstOrDefaultAsync(b => b.Inspections.Any(i => i.Id == request.InspectionId), cancellationToken)
            ?? throw new KeyNotFoundException($"Inspection {request.InspectionId} was not found.");

        var inspection = batch.RecordSafetyChecks(
            request.InspectionId,
            request.Checks.Select(c => new SafetyInput(
                c.SafetyTypeId, c.ResultId, c.Reason, (c.AffectedLines ?? []).Select(l => new LineRef(l.SizeId, l.ColourId)).ToList())).ToList(),
            request.ActingPerson);

        await dbContext.SaveChangesAsync(cancellationToken);
        return inspection.ToDto();
    }
}
