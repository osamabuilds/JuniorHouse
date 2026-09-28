using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Domain;
using Romp.Modules.Catalog.Contracts;
using Romp.Modules.Vendor.Domain;

namespace Romp.Modules.Vendor.Application;

/// <summary>
/// SCRUM-93 task 22 (ADR 0007, FR-SC-03). Proposes a new revision from a PO's current (In-force)
/// terms plus the requested changes - AC-9 (SentToVendor: immediately In-force) / AC-10
/// (Acknowledged: Pending) is decided by the handler from the PO's own status, not by the caller.
/// </summary>
public sealed record CreateAmendmentCommand(
    long PoId,
    short InitiatorId,
    short ReasonId,
    string ImpactNote,
    string? VendorMessage,
    decimal UnitCost,
    DateOnly ExpectedDeliveryDate,
    DateOnly? LatestAcceptableDate,
    decimal? OverTolerancePercent,
    decimal? UnderTolerancePercent,
    short PaymentTermId,
    decimal AdvancePercent,
    short? FabricResponsibilityId,
    IReadOnlyCollection<PoLineDto> Lines,
    IReadOnlyCollection<AmendmentFileAdd>? AddFiles = null,
    IReadOnlyCollection<long>? RetireFileIds = null) : IRequest<PoRevisionDto>, IVendorCommand;

/// <summary>SCRUM-93 task 37 (AC-38, AC-43). A vendor-visible file introduced by an amendment - the only way to add one after Send.</summary>
public sealed record AmendmentFileAdd(short CategoryId, string FileName, byte[] Content);

public sealed class CreateAmendmentCommandValidator : AbstractValidator<CreateAmendmentCommand>
{
    public CreateAmendmentCommandValidator(PoCommercialTermsOptions termsOptions, PoFileStorageOptions fileOptions)
    {
        RuleFor(c => c.UnitCost).GreaterThan(0);

        // AC-38/AC-45: same content rules as a direct upload; an amendment only ever adds vendor-visible files.
        RuleForEach(c => c.AddFiles).ChildRules(file =>
        {
            file.RuleFor(f => f.CategoryId).Must(id => Romp.Modules.Vendor.Domain.PoFileCategory.IsVendorVisible(id))
                .WithMessage("An amendment can only add vendor-visible files (tech pack, artwork, trim card, colour standard, packing instructions).");
            file.RuleFor(f => f.FileName).NotEmpty().WithMessage("Each added file needs a name.");
            file.RuleFor(f => f.Content)
                .Must(content => content.Length > 0).WithMessage("An added file is empty.")
                .Must(content => content.LongLength <= fileOptions.MaxFileSizeBytes)
                .WithMessage($"An added file is larger than the {fileOptions.MaxFileSizeBytes / (1024 * 1024)} MB limit.")
                .Must(content => content.Length == 0 || PoFileContent.DetectContentType(content) is not null)
                .WithMessage($"An added file has a type that isn't allowed. Use {PoFileContent.AllowedTypesDescription}.");
        });
        RuleFor(c => c.ReasonId).GreaterThan((short)0).WithMessage("A reason is required.");
        RuleFor(c => c.ImpactNote).NotEmpty().WithMessage("An internal impact note is required."); // AC-17
        RuleFor(c => c.Lines).NotEmpty().WithMessage("At least one size/colour line is required.");
        RuleForEach(c => c.Lines).ChildRules(line => line.RuleFor(l => l.Qty).GreaterThan(0));

        // AC-5 (same rules as UpdatePurchaseOrderCommand - these terms are amendable here too, R6).
        RuleFor(c => c.LatestAcceptableDate)
            .Must((command, latestAcceptableDate) =>
                latestAcceptableDate is null || latestAcceptableDate >= command.ExpectedDeliveryDate)
            .WithMessage("Latest acceptable date must be on or after the expected delivery date.");
        RuleFor(c => c.OverTolerancePercent)
            .Must(pct => pct is null || (pct >= 0 && pct <= termsOptions.MaxTolerancePercent))
            .WithMessage($"Over-ship tolerance must be between 0 and {termsOptions.MaxTolerancePercent}%.");
        RuleFor(c => c.UnderTolerancePercent)
            .Must(pct => pct is null || (pct >= 0 && pct <= termsOptions.MaxTolerancePercent))
            .WithMessage($"Under-ship tolerance must be between 0 and {termsOptions.MaxTolerancePercent}%.");
    }
}

public sealed class CreateAmendmentCommandHandler(IVendorDbContext dbContext, IStyleQueries styleQueries, IFileStorage fileStorage, PoFileStorageOptions fileOptions)
    : IRequestHandler<CreateAmendmentCommand, PoRevisionDto>
{
    public async Task<PoRevisionDto> Handle(CreateAmendmentCommand request, CancellationToken cancellationToken)
    {
        var po = await dbContext.PurchaseOrders
            .Include(p => p.Lines)
            .Include(p => p.Revisions).ThenInclude(r => r.Lines)
            .FirstOrDefaultAsync(p => p.Id == request.PoId, cancellationToken)
            ?? throw new KeyNotFoundException($"Purchase order {request.PoId} was not found.");

        // AC-20: Draft/Cancelled can't be amended - use Update (Draft) or nothing (Cancelled) instead.
        // PoStatus is internal to Romp.Modules.Vendor.Domain (a different assembly) - literals with
        // comments match this codebase's existing convention for referencing them from outside.
        if (po.StatusId is 1 or 4 /* Draft or Cancelled */)
        {
            throw new DomainException(
                $"PO {po.PoNo} cannot be amended while in its current status (only Sent to Vendor or Acknowledged can be amended).");
        }

        // AC-14: at most one open Pending revision at a time. RevisionStatus is likewise internal.
        var openPending = po.Revisions.FirstOrDefault(r => r.StatusId == 1 /* Pending */);
        if (openPending is not null)
        {
            throw new Romp.BuildingBlocks.Application.ValidationException(new Dictionary<string, string[]>
            {
                [nameof(request.PoId)] = [$"PO {po.PoNo} already has an open Pending revision (Rev {openPending.RevisionNumber}) - decide or withdraw it before proposing another."],
            });
        }

        // AC-15: line changes are validated against the style's size run/colourways, same rule as
        // Sprint 1's own PO creation (AC-8) - vendor and style themselves have no field on this
        // command at all, so they're structurally not amendable rather than checked here.
        var style = await styleQueries.FindActiveStyleAsync(po.StyleId, cancellationToken);
        var invalidLines = style is null
            ? request.Lines
            : request.Lines.Where(l => !style.SizeIds.Contains(l.SizeId) || !style.ColourIds.Contains(l.ColourId)).ToList();
        if (invalidLines.Count > 0)
        {
            throw new Romp.BuildingBlocks.Application.ValidationException(new Dictionary<string, string[]>
            {
                [nameof(request.Lines)] = [$"{invalidLines.Count} line(s) reference a size or colour not in this PO's style."],
            });
        }

        // AC-16: a true no-op (identical terms and lines to the PO's current position) is rejected.
        var requestedLines = request.Lines.Select(l => (l.SizeId, l.ColourId, l.Qty)).ToList();
        var addFiles = request.AddFiles ?? [];
        var retireFileIds = request.RetireFileIds ?? [];
        if (IsNoOp(po, request, requestedLines) && addFiles.Count == 0 && retireFileIds.Count == 0)
        {
            throw new Romp.BuildingBlocks.Application.ValidationException(new Dictionary<string, string[]>
            {
                [nameof(request.PoId)] = ["This amendment doesn't change anything from the PO's current terms or lines."],
            });
        }

        // AC-38/AC-43: only a vendor-visible file that is effective right now can be retired, and the
        // PO's file count limit still applies to what the amendment would add.
        var poFiles = await dbContext.PurchaseOrderFiles.Where(f => f.PoId == po.Id && !f.IsDeleted).ToListAsync(cancellationToken);
        var currentRevisionNumber = po.Revisions.FirstOrDefault(r => r.StatusId == 2 /* InForce */)?.RevisionNumber ?? (short)0;
        var effectiveNow = PoFileEffectiveSet.At(poFiles, po.Revisions, currentRevisionNumber).Select(f => f.Id).ToHashSet();
        var notRetirable = retireFileIds.Where(id => !effectiveNow.Contains(id)).ToList();
        if (notRetirable.Count > 0)
        {
            throw new Romp.BuildingBlocks.Application.ValidationException(new Dictionary<string, string[]>
            {
                [nameof(request.RetireFileIds)] = [$"{notRetirable.Count} file(s) can't be retired: only vendor-visible files currently in effect on PO {po.PoNo} can be."],
            });
        }

        if (poFiles.Count + addFiles.Count > fileOptions.MaxFilesPerPo)
        {
            throw new Romp.BuildingBlocks.Application.ValidationException(new Dictionary<string, string[]>
            {
                [nameof(request.AddFiles)] = [$"PO {po.PoNo} would exceed the maximum of {fileOptions.MaxFilesPerPo} files."],
            });
        }

        // AC-9 vs AC-10: SentToVendor goes immediately In-force; Acknowledged sits Pending.
        var goesImmediatelyInForce = po.StatusId == 2 /* SentToVendor */;

        var revision = po.CreateRevision(
            request.InitiatorId,
            request.ReasonId,
            request.ImpactNote,
            request.VendorMessage,
            request.UnitCost,
            request.ExpectedDeliveryDate,
            request.LatestAcceptableDate,
            request.OverTolerancePercent,
            request.UnderTolerancePercent,
            request.PaymentTermId,
            request.AdvancePercent,
            request.FabricResponsibilityId,
            requestedLines,
            goesImmediatelyInForce);

        // The revision's id only exists once saved; the files' effective window refers to it (AC-43).
        await dbContext.SaveChangesAsync(cancellationToken);

        if (addFiles.Count > 0 || retireFileIds.Count > 0)
        {
            foreach (var added in addFiles)
            {
                var storageKey = await fileStorage.SaveAsync(added.Content, cancellationToken);
                dbContext.PurchaseOrderFiles.Add(new PurchaseOrderFile(
                    po.Id, added.CategoryId, PoFileContent.SanitiseFileName(added.FileName), storageKey,
                    PoFileContent.DetectContentType(added.Content)!, added.Content.LongLength, revision.Id));
            }

            foreach (var file in poFiles.Where(f => retireFileIds.Contains(f.Id)))
            {
                file.RetireIn(revision.Id);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return revision.ToDto();
    }

    private static bool IsNoOp(PurchaseOrder po, CreateAmendmentCommand request, List<(short SizeId, short ColourId, int Qty)> requestedLines)
    {
        var termsUnchanged =
            po.UnitCost == request.UnitCost &&
            po.ExpectedDeliveryDate == request.ExpectedDeliveryDate &&
            po.LatestAcceptableDate == request.LatestAcceptableDate &&
            po.OverTolerancePercent == request.OverTolerancePercent &&
            po.UnderTolerancePercent == request.UnderTolerancePercent &&
            po.PaymentTermId == request.PaymentTermId &&
            po.AdvancePercent == request.AdvancePercent &&
            po.FabricResponsibilityId == request.FabricResponsibilityId;

        if (!termsUnchanged)
        {
            return false;
        }

        var currentLines = po.Lines.Select(l => (l.SizeId, l.ColourId, l.Qty)).ToHashSet();
        return currentLines.SetEquals(requestedLines);
    }
}
