using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.Modules.Reference.Domain;

namespace Romp.Modules.Reference.Application;

/// <summary>
/// AC-2: creates one row for a lookup type. <paramref name="ParentCategoryId"/>/
/// <paramref name="DefaultAdvancePercent"/> only apply to <c>CategoryLookup</c>/<c>PaymentTermLookup</c>
/// and are ignored for every other <typeparamref name="TLookup"/> (see <see cref="LookupFactory"/>).
/// Never registered for <c>PoStatusLookup</c>, which is system-owned (see ReferenceModule).
/// </summary>
public sealed record CreateLookupCommand<TLookup>(
    string Code,
    string Name,
    string? Description,
    short SortSeq,
    short? ParentCategoryId = null,
    decimal? DefaultAdvancePercent = null) : IRequest<LookupDto>, IReferenceCommand
    where TLookup : Lookup;

public sealed class CreateLookupCommandValidator<TLookup> : AbstractValidator<CreateLookupCommand<TLookup>>
    where TLookup : Lookup
{
    public CreateLookupCommandValidator()
    {
        RuleFor(c => c.Code).NotEmpty().MaximumLength(30);
        RuleFor(c => c.Name).NotEmpty().MaximumLength(100);
        RuleFor(c => c.Description).MaximumLength(500);
        RuleFor(c => c.SortSeq).GreaterThanOrEqualTo((short)0);
    }
}

public sealed class CreateLookupCommandHandler<TLookup>(IReferenceDbContext dbContext)
    : IRequestHandler<CreateLookupCommand<TLookup>, LookupDto>
    where TLookup : Lookup
{
    public async Task<LookupDto> Handle(CreateLookupCommand<TLookup> request, CancellationToken cancellationToken)
    {
        var alreadyExists = await dbContext.Lookups<TLookup>()
            .AnyAsync(lookup => lookup.Code == request.Code, cancellationToken);

        if (alreadyExists)
        {
            throw new Romp.BuildingBlocks.Application.ValidationException(
                new Dictionary<string, string[]> { [nameof(request.Code)] = [$"Code '{request.Code}' already exists."] });
        }

        var lookup = LookupFactory.Create<TLookup>(
            request.Code,
            request.Name,
            request.Description,
            request.SortSeq,
            request.ParentCategoryId,
            request.DefaultAdvancePercent);

        dbContext.Lookups<TLookup>().Add(lookup);

        // Saved here (not left to ReferenceTransactionBehavior) so the response DTO carries the
        // DB-generated Id rather than 0.
        await dbContext.SaveChangesAsync(cancellationToken);

        return lookup.ToDto();
    }
}
