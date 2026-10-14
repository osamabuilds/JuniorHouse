using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Romp.BuildingBlocks.Domain;
using Romp.Modules.Catalog.Application.Abstractions;
using Romp.Modules.Catalog.Application.Urls;
using Romp.Modules.Catalog.Domain.Products;
using Romp.Modules.Labelling.Contracts;
using Romp.Modules.Reference.Contracts;

namespace Romp.Modules.Catalog.Application.Products;

/// <summary>
/// Creates the product for a style (PRD-1, D1): one planned variant for every colour × size of the style, priced from the style's
/// target retail price when it has one (Q-PRD-9). Labelling's SKUs and GTINs are copied in through its contract (ADR 0014); this
/// feature never mints them. A style that already has a product is refused (409), also under a race (unique index).
/// </summary>
public sealed record CreateProductCommand(
    long StyleId, string Name, string Description, short GenderId, short CategoryId, short AgeBracketId, string ActingPerson)
    : IRequest<ProductDto>, ICatalogCommand;

public sealed class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator()
    {
        RuleFor(c => c.StyleId).GreaterThan(0);
        RuleFor(c => c.Name).NotEmpty().MaximumLength(200);
        RuleFor(c => c.Description).MaximumLength(2000);
        RuleFor(c => c.GenderId).GreaterThan((short)0);
        RuleFor(c => c.CategoryId).GreaterThan((short)0);
        RuleFor(c => c.AgeBracketId).GreaterThan((short)0);
        RuleFor(c => c.ActingPerson).ActingPerson();
    }
}

public sealed class CreateProductCommandHandler(
    ICatalogDbContext dbContext,
    ICatalogLookupQueries lookups,
    IVariantQueries labelling,
    LabelledVariantWriter labelledWriter,
    ProductAssembler assembler,
    IOptions<CatalogOptions> options)
    : IRequestHandler<CreateProductCommand, ProductDto>
{
    public async Task<ProductDto> Handle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        var style = await dbContext.Styles.AsNoTracking().AsSplitQuery()
            .Include(s => s.Colourways).Include(s => s.Sizes)
            .FirstOrDefaultAsync(s => s.Id == request.StyleId, cancellationToken)
            ?? throw new KeyNotFoundException($"Style {request.StyleId} was not found.");
        if (!style.IsActive)
        {
            throw new DomainException($"Style {style.Code} is not active, so it cannot get a product.");
        }

        var existing = await dbContext.Products.AsNoTracking().Where(p => p.StyleId == request.StyleId).Select(p => (long?)p.Id).FirstOrDefaultAsync(cancellationToken);
        if (existing is not null)
        {
            throw new DomainException($"Style {style.Code} already has a product (product {existing}).");
        }

        await ProductLookups.EnsureKnownAsync(lookups, request.GenderId, request.CategoryId, request.AgeBracketId, cancellationToken);

        // One slug at a time across concurrent creates, so equal names get stable, distinct slugs (URL-4).
        await dbContext.AcquireSlugLockAsync(cancellationToken);
        var slug = await new SlugSuggester(dbContext, options.Value).FirstFreeAsync(request.Name, request.GenderId, request.CategoryId, null, cancellationToken);

        var defaultPrice = style.TargetRetailPrice > 0 && style.TargetRetailPrice <= PlannedVariant.MaxPrice ? style.TargetRetailPrice : (decimal?)null;
        var product = Product.Create(
            style.Id, request.Name, request.Description, request.GenderId, request.CategoryId, request.AgeBracketId, slug,
            style.Colourways.Select(c => c.ColourId), style.Sizes.Select(s => s.SizeId), defaultPrice);
        dbContext.Products.Add(product);

        await labelledWriter.UpsertAsync(await labelling.GetByStyleAsync(style.Id, cancellationToken), cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await assembler.BuildByIdAsync(product.Id, cancellationToken);
    }
}

/// <summary>Checks that gender, category and age bracket ids exist in the reference data (one query each).</summary>
internal static class ProductLookups
{
    public static async Task EnsureKnownAsync(
        ICatalogLookupQueries lookups, short genderId, short categoryId, short ageBracketId, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (!(await lookups.GetGendersAsync([genderId], cancellationToken)).ContainsKey(genderId))
        {
            errors["GenderId"] = ["Choose a gender from the list."];
        }

        if (!(await lookups.GetCategoriesAsync([categoryId], cancellationToken)).ContainsKey(categoryId))
        {
            errors["CategoryId"] = ["Choose a category from the list."];
        }

        if (!(await lookups.GetAgeBracketsAsync([ageBracketId], cancellationToken)).ContainsKey(ageBracketId))
        {
            errors["AgeBracketId"] = ["Choose an age group from the list."];
        }

        if (errors.Count > 0)
        {
            throw new Romp.BuildingBlocks.Application.ValidationException(errors);
        }
    }
}
