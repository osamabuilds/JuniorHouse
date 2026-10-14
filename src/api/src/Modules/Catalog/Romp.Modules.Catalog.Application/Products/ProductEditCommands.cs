using FluentValidation;
using MediatR;
using Romp.BuildingBlocks.Domain;
using Romp.Modules.Catalog.Application.Abstractions;
using Romp.Modules.Catalog.Application.Urls;
using Romp.Modules.Catalog.Domain.Products;
using Romp.Modules.Reference.Contracts;

namespace Romp.Modules.Catalog.Application.Products;

/// <summary>
/// Edits the descriptive content (PRD-5: a stale <c>Version</c> is a 409). Gender and category are part of the public path: on a
/// published product they change only through the URL tab, which keeps the old address working (ADR 0013).
/// </summary>
public sealed record UpdateProductContentCommand(
    long ProductId, string Name, string Description, short GenderId, short CategoryId, short AgeBracketId, string? CareNote, uint Version, string ActingPerson)
    : IRequest<ProductDto>, ICatalogCommand;

public sealed class UpdateProductContentCommandValidator : AbstractValidator<UpdateProductContentCommand>
{
    public UpdateProductContentCommandValidator()
    {
        RuleFor(c => c.Name).NotEmpty().MaximumLength(200);
        RuleFor(c => c.Description).MaximumLength(2000);
        RuleFor(c => c.GenderId).GreaterThan((short)0);
        RuleFor(c => c.CategoryId).GreaterThan((short)0);
        RuleFor(c => c.AgeBracketId).GreaterThan((short)0);
        RuleFor(c => c.CareNote).Must(note => Product.CheckCareNote(note) is null).WithMessage((_, note) => Product.CheckCareNote(note) ?? string.Empty);
        RuleFor(c => c.ActingPerson).ActingPerson();
    }
}

public sealed class UpdateProductContentCommandHandler(
    ICatalogDbContext dbContext, ICatalogLookupQueries lookups, ProductAssembler assembler, ProductPathWriter pathWriter, IProductPathBuilder paths)
    : IRequestHandler<UpdateProductContentCommand, ProductDto>
{
    public async Task<ProductDto> Handle(UpdateProductContentCommand request, CancellationToken cancellationToken)
    {
        var product = await assembler.LoadAsync(request.ProductId, cancellationToken);
        dbContext.ExpectVersion(product, request.Version);
        await ProductLookups.EnsureKnownAsync(lookups, request.GenderId, request.CategoryId, request.AgeBracketId, cancellationToken);

        if (product.GenderId != request.GenderId || product.CategoryId != request.CategoryId)
        {
            if (product.Status == ProductStatus.Published)
            {
                throw new DomainException("Gender and category are part of the product's URL. Change them on the URL tab, which keeps the old address working.");
            }

            await pathWriter.MoveAsync(product, request.GenderId, request.CategoryId, product.Slug, cancellationToken);
        }

        product.UpdateContent(request.Name, request.Description, request.GenderId, request.CategoryId, request.AgeBracketId, request.CareNote);
        if (product.Status == ProductStatus.Published)
        {
            product.NoteChanged(await paths.PathOfAsync(product.GenderId, product.CategoryId, product.Slug, cancellationToken));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return await assembler.BuildAsync(product, cancellationToken);
    }
}

/// <summary>
/// Sets the price and optional compare-at ("was") price of variants (PRD-2, PRD-7, PRD-8, D4). Each change is kept in the price
/// history with who and when. Variants not listed keep their prices. All amounts are PKR, two decimals.
/// </summary>
public sealed record SetVariantPricesCommand(long ProductId, IReadOnlyList<VariantPriceInput> Prices, uint Version, string ActingPerson)
    : IRequest<ProductDto>, ICatalogCommand;

public sealed class SetVariantPricesCommandValidator : AbstractValidator<SetVariantPricesCommand>
{
    public SetVariantPricesCommandValidator()
    {
        RuleFor(c => c.Prices).NotEmpty().Must(p => p.Count <= 500).WithMessage("Send at most 500 prices at a time.");
        RuleForEach(c => c.Prices)
            .Must(p => PlannedVariant.CheckPrices(p.Price, p.CompareAtPrice) is null)
            .WithMessage((_, p) => PlannedVariant.CheckPrices(p.Price, p.CompareAtPrice) ?? string.Empty);
        RuleFor(c => c.ActingPerson).ActingPerson();
    }
}

public sealed class SetVariantPricesCommandHandler(ICatalogDbContext dbContext, ProductAssembler assembler, IProductPathBuilder paths)
    : IRequestHandler<SetVariantPricesCommand, ProductDto>
{
    public async Task<ProductDto> Handle(SetVariantPricesCommand request, CancellationToken cancellationToken)
    {
        var product = await assembler.LoadAsync(request.ProductId, cancellationToken);
        dbContext.ExpectVersion(product, request.Version);

        var history = product.SetPrices(request.Prices.Select(p => (p.ColourId, p.SizeId, p.Price, p.CompareAtPrice)), request.ActingPerson.Trim());
        dbContext.PriceHistory.AddRange(history);
        if (history.Count > 0)
        {
            dbContext.Touch(product);
            product.NoteChanged(await paths.PathOfAsync(product.GenderId, product.CategoryId, product.Slug, cancellationToken));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return await assembler.BuildAsync(product, cancellationToken);
    }
}

/// <summary>Replaces the collections a product belongs to (PRD-12): many collections, one category.</summary>
public sealed record SetCollectionsCommand(long ProductId, IReadOnlyList<short> CollectionIds, uint Version, string ActingPerson)
    : IRequest<ProductDto>, ICatalogCommand;

public sealed class SetCollectionsCommandValidator : AbstractValidator<SetCollectionsCommand>
{
    public SetCollectionsCommandValidator()
    {
        RuleFor(c => c.CollectionIds).NotNull().Must(ids => ids.Count <= 50).WithMessage("Choose at most 50 collections.");
        RuleFor(c => c.ActingPerson).ActingPerson();
    }
}

public sealed class SetCollectionsCommandHandler(
    ICatalogDbContext dbContext, ICatalogLookupQueries lookups, ProductAssembler assembler, IProductPathBuilder paths)
    : IRequestHandler<SetCollectionsCommand, ProductDto>
{
    public async Task<ProductDto> Handle(SetCollectionsCommand request, CancellationToken cancellationToken)
    {
        var product = await assembler.LoadAsync(request.ProductId, cancellationToken);
        dbContext.ExpectVersion(product, request.Version);

        var wanted = request.CollectionIds.Distinct().ToList();
        var known = await lookups.GetCollectionsAsync(wanted, cancellationToken);
        if (wanted.Any(id => !known.ContainsKey(id)))
        {
            throw new Romp.BuildingBlocks.Application.ValidationException(new Dictionary<string, string[]>
            {
                [nameof(request.CollectionIds)] = ["Choose collections from the list."],
            });
        }

        product.SetCollections(wanted);
        dbContext.Touch(product);
        product.NoteChanged(await paths.PathOfAsync(product.GenderId, product.CategoryId, product.Slug, cancellationToken));
        await dbContext.SaveChangesAsync(cancellationToken);
        return await assembler.BuildAsync(product, cancellationToken);
    }
}
