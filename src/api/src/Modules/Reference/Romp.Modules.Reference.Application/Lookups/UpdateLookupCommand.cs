using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.Modules.Reference.Application.Abstractions;
using Romp.Modules.Reference.Domain;
using Romp.Modules.Reference.Domain.Lookups;

namespace Romp.Modules.Reference.Application.Lookups;

/// <summary>AC-2: edits an existing row's name/description/sort order (and, where applicable, its extra field).</summary>
public sealed record UpdateLookupCommand<TLookup>(
    short Id,
    string Name,
    string? Description,
    short SortSeq,
    short? ParentCategoryId = null,
    decimal? DefaultAdvancePercent = null) : IRequest<LookupDto>, IReferenceCommand
    where TLookup : Lookup;

public sealed class UpdateLookupCommandValidator<TLookup> : AbstractValidator<UpdateLookupCommand<TLookup>>
    where TLookup : Lookup
{
    public UpdateLookupCommandValidator()
    {
        RuleFor(c => c.Name).NotEmpty().MaximumLength(100);
        RuleFor(c => c.Description).MaximumLength(500);
        RuleFor(c => c.SortSeq).GreaterThanOrEqualTo((short)0);
    }
}

public sealed class UpdateLookupCommandHandler<TLookup>(IReferenceDbContext dbContext)
    : IRequestHandler<UpdateLookupCommand<TLookup>, LookupDto>
    where TLookup : Lookup
{
    public async Task<LookupDto> Handle(UpdateLookupCommand<TLookup> request, CancellationToken cancellationToken)
    {
        var lookup = await dbContext.Lookups<TLookup>().FirstOrDefaultAsync(l => l.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"{typeof(TLookup).Name} {request.Id} was not found.");

        lookup.Update(request.Name, request.Description, request.SortSeq);
        LookupFactory.ApplyExtra(lookup, request.ParentCategoryId, request.DefaultAdvancePercent);

        // Saved here (not left to ReferenceTransactionBehavior) so the response DTO reflects
        // UPDT_DTE/BY as stamped by AuditSaveChangesInterceptor, not the pre-save in-memory value.
        await dbContext.SaveChangesAsync(cancellationToken);

        return lookup.ToDto();
    }
}
