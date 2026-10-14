using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.Modules.Labelling.Application.Abstractions;
using Romp.Modules.Labelling.Domain;

namespace Romp.Modules.Labelling.Application.Variants;

/// <summary>
/// Retires a variant's active GTIN at any time, with a reason (VAR-8, D8). The number is never issued again; units already tagged
/// are untouched; the variant's next accepted quantity gets the next number (VAR-9). Admin-ready: declared <c>Admin</c> at the endpoint.
/// </summary>
public sealed record VoidGtinCommand(long VariantId, short ReasonId, string? Note, string ActingPerson)
    : IRequest<VariantDetailDto>, ILabellingCommand;

public sealed class VoidGtinCommandValidator : AbstractValidator<VoidGtinCommand>
{
    public VoidGtinCommandValidator()
    {
        RuleFor(c => c.ReasonId).Must(VoidReasons.IsKnown).WithMessage("Choose why the GTIN is being voided.");
        RuleFor(c => c.Note).OptionalNote();
        RuleFor(c => c.ActingPerson).ActingPerson();
    }
}

public sealed class VoidGtinCommandHandler(
    ILabellingDbContext dbContext, IRowLocks locks, ILabelStyleLookup lookup, TimeProvider timeProvider)
    : IRequestHandler<VoidGtinCommand, VariantDetailDto>
{
    public async Task<VariantDetailDto> Handle(VoidGtinCommand request, CancellationToken cancellationToken)
    {
        await locks.LockVariantAsync(request.VariantId, cancellationToken); // serialises with a concurrent assign or void

        var variant = await dbContext.Variants.Include(v => v.Gtins)
            .FirstOrDefaultAsync(v => v.Id == request.VariantId, cancellationToken)
            ?? throw new KeyNotFoundException($"Variant {request.VariantId} was not found.");

        variant.Void(request.ReasonId, request.Note?.Trim(), request.ActingPerson.Trim(), timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetVariantQueryHandler.BuildDetailAsync(variant, lookup, cancellationToken);
    }
}
