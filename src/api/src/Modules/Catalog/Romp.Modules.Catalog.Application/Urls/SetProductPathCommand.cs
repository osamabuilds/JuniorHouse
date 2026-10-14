using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Romp.BuildingBlocks.Domain;
using Romp.Modules.Catalog.Application.Abstractions;
using Romp.Modules.Catalog.Domain.Products;
using Romp.Modules.Catalog.Domain.Urls;
using Romp.Modules.Reference.Contracts;

namespace Romp.Modules.Catalog.Application.Urls;

/// <summary>A product's path after a change, with the token to send back with the next edit.</summary>
public sealed record ProductPathDto(long ProductId, short GenderId, short CategoryId, string Slug, string Path, uint Version);

/// <summary>
/// Changes a product's gender, category and slug (URL-1..3, URL-7, URL-8, URL-11). A product that has ever been public keeps the
/// old address in its history, so the old path answers with a 301 to the new one in one hop. A published product also writes
/// <c>ProductChangedEvent</c> carrying both paths, in the same transaction.
/// </summary>
public sealed record SetProductPathCommand(long ProductId, short GenderId, short CategoryId, string Slug, uint Version, string ActingPerson)
    : IRequest<ProductPathDto>, ICatalogCommand;

public sealed class SetProductPathCommandValidator : AbstractValidator<SetProductPathCommand>
{
    public SetProductPathCommandValidator(IOptions<CatalogOptions> options)
    {
        var catalog = options.Value;
        RuleFor(c => c.GenderId).GreaterThan((short)0);
        RuleFor(c => c.CategoryId).GreaterThan((short)0);
        RuleFor(c => c.Slug)
            .Must((_, slug) => SlugPolicy.Validate(slug, catalog.ReservedSlugs, catalog.MaxSlugLength) is null)
            .WithMessage((_, slug) => SlugPolicy.Validate(slug, catalog.ReservedSlugs, catalog.MaxSlugLength) ?? string.Empty);
        RuleFor(c => c.ActingPerson).ActingPerson();
    }
}

public sealed class SetProductPathCommandHandler(
    ICatalogDbContext dbContext, ICatalogLookupQueries lookups, IProductPathBuilder paths, ProductPathWriter writer)
    : IRequestHandler<SetProductPathCommand, ProductPathDto>
{
    public async Task<ProductPathDto> Handle(SetProductPathCommand request, CancellationToken cancellationToken)
    {
        var product = await dbContext.Products.FirstOrDefaultAsync(p => p.Id == request.ProductId, cancellationToken)
            ?? throw new KeyNotFoundException($"Product {request.ProductId} was not found.");
        dbContext.ExpectVersion(product, request.Version);

        if (!(await lookups.GetGendersAsync([request.GenderId], cancellationToken)).ContainsKey(request.GenderId)
            || !(await lookups.GetCategoriesAsync([request.CategoryId], cancellationToken)).ContainsKey(request.CategoryId))
        {
            throw new Romp.BuildingBlocks.Application.ValidationException(new Dictionary<string, string[]>
            {
                [nameof(request.GenderId)] = ["Choose a gender and a category from the lists."],
            });
        }

        await writer.MoveAsync(product, request.GenderId, request.CategoryId, request.Slug, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        var path = await paths.PathOfAsync(product.GenderId, product.CategoryId, product.Slug, cancellationToken);
        return new ProductPathDto(product.Id, product.GenderId, product.CategoryId, product.Slug, path, dbContext.VersionOf(product));
    }
}

/// <summary>
/// The one place a product's path changes (used by the Urls command and by product creation/editing): refuses a path another product
/// owns or ever owned (URL-11), applies the change, keeps history for a product that has been public, and tells the storefront.
/// </summary>
public sealed class ProductPathWriter(ICatalogDbContext dbContext, IProductPathBuilder paths)
{
    public async Task MoveAsync(Product product, short genderId, short categoryId, string slug, CancellationToken cancellationToken)
    {
        var unchanged = product.GenderId == genderId && product.CategoryId == categoryId && product.Slug == slug;
        if (unchanged)
        {
            return;
        }

        var taken = await dbContext.Products.AnyAsync(
                p => p.Id != product.Id && p.GenderId == genderId && p.CategoryId == categoryId && p.Slug == slug, cancellationToken)
            || await dbContext.PathHistory.AnyAsync(
                h => h.ProductId != product.Id && h.GenderId == genderId && h.CategoryId == categoryId && h.Slug == slug, cancellationToken);
        if (taken)
        {
            var path = await paths.PathOfAsync(genderId, categoryId, slug, cancellationToken);
            throw new DomainException($"The address {path} is already used by another product (or was). Choose a different URL name.");
        }

        var oldPath = product.Status == ProductStatus.Published
            ? await paths.PathOfAsync(product.GenderId, product.CategoryId, product.Slug, cancellationToken)
            : null;

        product.ChangePath(genderId, categoryId, slug);

        // A product that has been public keeps every address it had; the new one is recorded now so it resolves straight away.
        if (product.PublishedAt is not null)
        {
            await EnsureHistoryAsync(product, cancellationToken);
        }

        if (oldPath is not null)
        {
            product.NoteChanged(await paths.PathOfAsync(genderId, categoryId, slug, cancellationToken), oldPath);
        }
    }

    /// <summary>Writes the product's current address to its history if it is not there yet (done at publish and on a path change).</summary>
    public async Task EnsureHistoryAsync(Product product, CancellationToken cancellationToken)
    {
        var exists = await dbContext.PathHistory.AnyAsync(
            h => h.ProductId == product.Id && h.GenderId == product.GenderId && h.CategoryId == product.CategoryId && h.Slug == product.Slug,
            cancellationToken);
        if (!exists)
        {
            dbContext.PathHistory.Add(new PathHistoryEntry(product.Id, product.GenderId, product.CategoryId, product.Slug));
        }
    }
}
