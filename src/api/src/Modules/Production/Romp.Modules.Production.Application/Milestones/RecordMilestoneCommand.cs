using FluentValidation;
using MediatR;
using Romp.BuildingBlocks.Application;
using Romp.Modules.Production.Application.Abstractions;
using Romp.Modules.Production.Application.ProductionRuns;
using Romp.Modules.Production.Domain.Milestones;
using Romp.Modules.Vendor.Contracts;

namespace Romp.Modules.Production.Application.Milestones;

/// <summary>
/// Records a milestone the vendor reported (spec AC-4..AC-9, AC-17, AC-30, AC-35). PP-sample
/// decisions have their own commands. Recording <c>BulkCuttingStarted</c> also raises the event that
/// moves the PO to In Production (D1).
/// </summary>
public sealed record RecordMilestoneCommand(
    long RunId,
    short TypeId,
    DateOnly ClaimedDate,
    string ReportedBy,
    string ActingPerson,
    string? Note,
    IReadOnlyList<QuantityLineDto>? FinishedLines = null) : IRequest<MilestoneDto>, IProductionCommand;

public sealed class RecordMilestoneCommandValidator : AbstractValidator<RecordMilestoneCommand>
{
    public RecordMilestoneCommandValidator()
    {
        RuleFor(c => c.TypeId).Must(MilestoneType.IsGeneric)
            .WithMessage("Choose a milestone to record. Sample approvals are recorded on the sample round.");
        RuleFor(c => c.ReportedBy).NotEmpty().WithMessage("Enter who at the vendor reported this.").MaximumLength(100);
        RuleFor(c => c.ActingPerson).ActingPerson();
        RuleFor(c => c.Note).MaximumLength(500);
        RuleForEach(c => c.FinishedLines).ChildRules(line => line.RuleFor(l => l.Qty).GreaterThan(0).WithMessage("Finished quantities must be positive whole numbers."));
    }
}

public sealed class RecordMilestoneCommandHandler(
    IProductionDbContext dbContext, IPurchaseOrderQueries poQueries, ProductionOptions options, TimeProvider timeProvider)
    : IRequestHandler<RecordMilestoneCommand, MilestoneDto>
{
    public async Task<MilestoneDto> Handle(RecordMilestoneCommand request, CancellationToken cancellationToken)
    {
        var run = await dbContext.LoadRunAsync(request.RunId, cancellationToken);

        // AC-17: finished quantities must be for sizes and colours that are on the PO.
        if (request.TypeId == MilestoneType.FinishedReadyToShip && request.FinishedLines is { Count: > 0 })
        {
            var terms = await poQueries.GetInForceTermsAsync(run.PoId, cancellationToken);
            var unknown = request.FinishedLines.Count(l => terms is null || terms.Lines.All(o => o.SizeId != l.SizeId || o.ColourId != l.ColourId));
            if (unknown > 0)
            {
                throw new Romp.BuildingBlocks.Application.ValidationException(new Dictionary<string, string[]>
                {
                    [nameof(request.FinishedLines)] = [$"{unknown} finished line(s) use a size or colour that is not on PO {run.PoNo}."],
                });
            }
        }

        var milestone = run.RecordMilestone(
            request.TypeId, request.ClaimedDate, request.ReportedBy, request.ActingPerson, request.Note,
            PakistanClock.Today(timeProvider), options.RecordedLateDays,
            request.FinishedLines?.Select(l => (l.SizeId, l.ColourId, l.Qty)).ToList());

        // Saved here (not left to the transaction behaviour) so the response carries the real id and audit stamps.
        await dbContext.SaveChangesAsync(cancellationToken);
        return milestone.ToDto();
    }
}
