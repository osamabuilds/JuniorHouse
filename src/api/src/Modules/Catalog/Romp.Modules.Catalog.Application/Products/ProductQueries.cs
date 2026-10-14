using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Application.Paging;
using Romp.Modules.Catalog.Application.Abstractions;
using Romp.Modules.Catalog.Domain.Products;

namespace Romp.Modules.Catalog.Application.Products;

/// <summary>
/// The paged product list (PRD-11): filter by status, category and a search over the name or style code. Ordered by id, which is
/// unique, so pages never overlap. One COUNT and one page query: the style code and both variant counts are part of the same SELECT.
/// </summary>
public sealed record ListProductsQuery(string? Search = null, short? StatusId = null, short? CategoryId = null, int Page = 1, int PageSize = PageRequest.DefaultPageSize)
    : IRequest<PagedResult<ProductRowDto>>;

public sealed class ListProductsQueryValidator : AbstractValidator<ListProductsQuery>
{
    public ListProductsQueryValidator()
    {
        RuleFor(q => q.Search).MaximumLength(ValidationRules.MaxSearchLength);
        RuleFor(q => q.StatusId).Must(id => id is null || Enum.IsDefined((ProductStatus)id.Value)).WithMessage("Unknown status.");
    }
}

public sealed class ListProductsQueryHandler(ICatalogDbContext dbContext) : IRequestHandler<ListProductsQuery, PagedResult<ProductRowDto>>
{
    public async Task<PagedResult<ProductRowDto>> Handle(ListProductsQuery request, CancellationToken cancellationToken)
    {
        var products = dbContext.Products.AsNoTracking().AsQueryable();
        if (request.StatusId is { } statusId)
        {
            var status = statusId;
            products = products.Where(p => p.StatusId == status);
        }

        if (request.CategoryId is { } categoryId)
        {
            products = products.Where(p => p.CategoryId == categoryId);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
#pragma warning disable CA1304, CA1311, CA1862 // translated to SQL lower(), which has no culture; the StringComparison overload is not translatable
            var text = request.Search.Trim().ToLower();
            products = products.Where(p => p.Name.ToLower().Contains(text)
                || dbContext.Styles.Any(s => s.Id == p.StyleId && s.Code.ToLower().Contains(text)));
        }
#pragma warning restore CA1304, CA1311, CA1862

        var page = await products
            .OrderByDescending(p => p.Id)
            .Select(p => new
            {
                p.Id,
                p.StyleId,
                StyleCode = dbContext.Styles.Where(s => s.Id == p.StyleId).Select(s => s.Code).FirstOrDefault() ?? string.Empty,
                p.Name,
                p.GenderId,
                p.CategoryId,
                p.StatusId,
                p.Slug,
                Planned = p.Variants.Count,
                Sellable = p.Variants.Count(v => v.Price > 0
                    && dbContext.LabelledVariants.Any(l => l.StyleId == p.StyleId && l.ColourId == v.ColourId && l.SizeId == v.SizeId && !l.GtinVoided)),
                p.PublishedAt,
            })
            .ToPagedResultAsync(new PageRequest(request.Page, request.PageSize), cancellationToken);

        return new PagedResult<ProductRowDto>(
            page.Items.Select(r => new ProductRowDto(
                r.Id, r.StyleId, r.StyleCode, r.Name, r.GenderId, r.CategoryId, r.StatusId, ((ProductStatus)r.StatusId).ToString(), r.Slug, r.Planned, r.Sellable, r.PublishedAt)).ToList(),
            page.Page, page.PageSize, page.TotalCount);
    }
}

/// <summary>One product with its variants (planned, with SKU and GTIN where Labelling has issued them) and a preview of the publish gate (PRD-1).</summary>
public sealed record GetProductQuery(long ProductId) : IRequest<ProductDto>;

public sealed class GetProductQueryHandler(ProductAssembler assembler) : IRequestHandler<GetProductQuery, ProductDto>
{
    public Task<ProductDto> Handle(GetProductQuery request, CancellationToken cancellationToken) =>
        assembler.BuildByIdAsync(request.ProductId, cancellationToken);
}
