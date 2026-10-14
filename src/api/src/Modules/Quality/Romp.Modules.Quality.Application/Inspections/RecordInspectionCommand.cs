using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.Modules.Quality.Application.Abstractions;
using Romp.Modules.Quality.Application.Batches;
using Romp.Modules.Quality.Application.Inspections.Aql;
using Romp.Modules.Quality.Domain.Batches;
using Romp.Modules.Quality.Domain.Inspections;
using Romp.Modules.Quality.Domain.Inspections.Aql;

namespace Romp.Modules.Quality.Application.Inspections;

/// <summary>
/// Records an inspection round on a batch (or on a sub-batch returned from rework), spec AC-13..AC-18, AC-30. The plan
/// (sample size, accept/reject numbers) comes from the seeded AQL tables through <see cref="AqlCalculator"/>; the result is
/// computed by the domain, never typed in. Writes <c>QcInspectionRecorded</c> to the outbox in the same transaction.
/// The inspection and AQL level default to <c>Qc:DefaultInspectionLevelCode</c> / <c>Qc:DefaultAqlLevelCode</c>.
/// </summary>
public sealed record RecordInspectionCommand(
    long BatchId,
    long? SubBatchId,
    DateOnly InspectionDate,
    string ActingPerson,
    int? SampleQty,
    short? InspectionLevelId,
    short? AqlLevelId,
    IReadOnlyList<CheckResultDto> Checks,
    IReadOnlyList<DefectDto> Defects) : IRequest<InspectionDto>, IQualityCommand;

public sealed class RecordInspectionCommandValidator : AbstractValidator<RecordInspectionCommand>
{
    public RecordInspectionCommandValidator()
    {
        RuleFor(c => c.ActingPerson).ActingPerson();
        RuleFor(c => c.Checks).NotNull().WithMessage("Record a result for every checklist item.");
        RuleFor(c => c.Defects).NotNull().WithMessage("Send the defect list (empty when none were found).");
        RuleForEach(c => c.Defects).ChildRules(defect =>
        {
            defect.RuleFor(d => d.Qty).GreaterThan(0).WithMessage("Each defect quantity must be a positive whole number.");
            defect.RuleFor(d => d.Note).NotEmpty().WithMessage("Every defect needs a written note.").MaximumLength(500);
        });
        RuleFor(c => c.SampleQty).GreaterThan(0).When(c => c.SampleQty is not null).WithMessage("The inspected quantity must be a positive whole number.");
    }
}

public sealed class RecordInspectionCommandHandler(IQualityDbContext dbContext, QualityOptions options, TimeProvider timeProvider)
    : IRequestHandler<RecordInspectionCommand, InspectionDto>
{
    public async Task<InspectionDto> Handle(RecordInspectionCommand request, CancellationToken cancellationToken)
    {
        var batch = await dbContext.Batches
            .Include(b => b.Lines)
            .Include(b => b.SubBatches).ThenInclude(s => s.Lines)
            .Include(b => b.Inspections)
            .FirstOrDefaultAsync(b => b.Id == request.BatchId, cancellationToken)
            ?? throw new KeyNotFoundException($"QC batch {request.BatchId} was not found.");

        var levelId = request.InspectionLevelId ?? PlanCodes.InspectionLevelId(options.DefaultInspectionLevelCode);
        var aqlId = request.AqlLevelId ?? PlanCodes.AqlLevelId(options.DefaultAqlLevelCode);
        var table = await AqlPlanLoader.LoadAsync(dbContext, cancellationToken);
        var plan = AqlCalculator.Plan(table, batch.ScopeQty(request.SubBatchId), levelId, aqlId, options.FullInspectionMaxLot);

        var inspection = batch.RecordInspection(
            new InspectionInput(
                request.SubBatchId, request.InspectionDate, request.ActingPerson, request.SampleQty, levelId, aqlId,
                request.Checks.Select(c => new CheckInput(c.CheckTypeId, c.ResultId)).ToList(),
                request.Defects.Select(d => new DefectInput(d.DefectTypeId, d.SeverityId, d.Qty, d.SizeId, d.ColourId, d.Note)).ToList()),
            plan, options.CriticalDefectTolerance, PakistanClock.Today(timeProvider));

        await dbContext.SaveChangesAsync(cancellationToken); // the event is written with the id of the new round known to the batch
        return inspection.ToDto();
    }
}
