using FluentValidation;
using MediatR;
using Romp.BuildingBlocks.Application;
using Romp.Modules.Production.Application.Abstractions;
using Romp.Modules.Production.Application.ProductionRuns;
using Romp.Modules.Production.Domain.Deliveries;
using Romp.Modules.Vendor.Contracts;

namespace Romp.Modules.Production.Application.Deliveries;

/// <summary>
/// Records a vendor delivery note against the run's PO (FR-SC-04 part 1, spec AC-18..AC-24, AC-31, AC-35, AC-37).
/// Judged on entry against the PO revision in force on the <b>dispatch</b> date (D12). Over-delivery is recorded
/// and flagged, never refused (D4). The outcome (partially delivered / delivered) goes to the PO by an event in the same transaction.
/// </summary>
public sealed record RecordDeliveryNoteCommand(
    long RunId,
    string VendorDnNo,
    DateOnly DispatchDate,
    bool IsFinalShipment,
    IReadOnlyList<QuantityLineDto> Lines,
    string ActingPerson) : IRequest<DeliveryNoteDto>, IProductionCommand;

public sealed class RecordDeliveryNoteCommandValidator : AbstractValidator<RecordDeliveryNoteCommand>
{
    public RecordDeliveryNoteCommandValidator()
    {
        RuleFor(c => c.VendorDnNo).NotEmpty().WithMessage("Enter the vendor's delivery note number.").MaximumLength(50);
        RuleFor(c => c.ActingPerson).ActingPerson();
        RuleFor(c => c.Lines).NotEmpty().WithMessage("Add at least one delivered size and colour.");
        RuleForEach(c => c.Lines).ChildRules(line => line.RuleFor(l => l.Qty).GreaterThan(0).WithMessage("Each delivered quantity must be a positive whole number."));
    }
}

public sealed class RecordDeliveryNoteCommandHandler(
    IProductionDbContext dbContext, IPurchaseOrderQueries poQueries, TimeProvider timeProvider)
    : IRequestHandler<RecordDeliveryNoteCommand, DeliveryNoteDto>
{
    public async Task<DeliveryNoteDto> Handle(RecordDeliveryNoteCommand request, CancellationToken cancellationToken)
    {
        var run = await dbContext.LoadRunAsync(request.RunId, cancellationToken);

        // D12: the terms as they stood on the dispatch date, not on the day the note is entered.
        var terms = await poQueries.GetTermsAtAsync(run.PoId, request.DispatchDate, cancellationToken)
            ?? throw new Romp.BuildingBlocks.Application.ValidationException(new Dictionary<string, string[]>
            {
                [nameof(request.RunId)] = [$"The terms of PO {run.PoNo} could not be found."],
            });

        var deliveryTerms = new DeliveryTerms(
            terms.RevisionNumber, terms.ExpectedDeliveryDate, terms.LatestAcceptableDate, terms.OverTolerancePercent, terms.UnderTolerancePercent,
            terms.Lines.Select(l => new OrderedLine(l.SizeId, l.ColourId, l.Qty)).ToList());

        var note = run.RecordDeliveryNote(
            request.VendorDnNo, request.DispatchDate, request.IsFinalShipment,
            request.Lines.Select(l => new NoteLineInput(l.SizeId, l.ColourId, l.Qty)).ToList(),
            deliveryTerms, request.ActingPerson, PakistanClock.Today(timeProvider));

        await dbContext.SaveChangesAsync(cancellationToken);
        return note.ToDto();
    }
}

public sealed record GetDeliveryNotesQuery(long RunId) : IRequest<IReadOnlyList<DeliveryNoteDto>>;

public sealed class GetDeliveryNotesQueryHandler(IProductionDbContext dbContext)
    : IRequestHandler<GetDeliveryNotesQuery, IReadOnlyList<DeliveryNoteDto>>
{
    public async Task<IReadOnlyList<DeliveryNoteDto>> Handle(GetDeliveryNotesQuery request, CancellationToken cancellationToken)
    {
        var run = await dbContext.LoadRunAsync(request.RunId, cancellationToken);
        return run.DeliveryNotes.OrderBy(n => n.DispatchDate).ThenBy(n => n.Id).Select(n => n.ToDto()).ToList();
    }
}
