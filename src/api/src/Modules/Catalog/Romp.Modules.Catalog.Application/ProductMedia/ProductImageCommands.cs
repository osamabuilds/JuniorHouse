using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Romp.BuildingBlocks.Domain;
using Romp.BuildingBlocks.Storage;
using Romp.Modules.Catalog.Application.Abstractions;
using Romp.Modules.Catalog.Application.Urls;
using Romp.Modules.Catalog.Domain.ProductMedia;
using Romp.Modules.Catalog.Domain.Products;
using Romp.Modules.Reference.Contracts;

namespace Romp.Modules.Catalog.Application.ProductMedia;

/// <summary>
/// An image as the editor and the storefront need it (MED-4, MED-10): the alt text, the dimensions for layout, and a suggestion staff
/// can edit. <c>AltTextProblem</c> is why the current alt text would block a publish, or null when it is fine.
/// </summary>
public sealed record ProductImageDto(
    long Id,
    long ProductId,
    short? ColourId,
    string FileName,
    string ContentType,
    int ByteSize,
    int Width,
    int Height,
    int SortSeq,
    bool IsMain,
    string? AltText,
    string SuggestedAltText,
    string? AltTextProblem);

/// <summary>Files to remove from storage once the database transaction has committed (a failed removal is logged, never rolled back).</summary>
public sealed class PendingFileDeletes
{
    private readonly List<string> _keys = [];

    public IReadOnlyList<string> Keys => _keys;

    public void Add(string key) => _keys.Add(key);

    public void Clear() => _keys.Clear();
}

/// <summary>Reads image rows into DTOs with the suggested alt text (one lookup call for the colours).</summary>
public sealed class ProductImageMapper(ICatalogLookupQueries lookups)
{
    public async Task<IReadOnlyList<ProductImageDto>> MapAsync(string productName, IReadOnlyList<ProductImage> images, CancellationToken cancellationToken)
    {
        var colours = await lookups.GetColoursAsync(images.Where(i => i.ColourId.HasValue).Select(i => i.ColourId!.Value).Distinct().ToList(), cancellationToken);
        return images
            .OrderBy(i => i.SortSeq).ThenBy(i => i.Id)
            .Select((image, index) => new ProductImageDto(
                image.Id, image.ProductId, image.ColourId, image.OriginalFileName, image.ContentType, image.ByteSize, image.Width, image.Height,
                image.SortSeq, index == 0, image.AltText,
                AltText.Suggest(productName, image.ColourId is { } c && colours.TryGetValue(c, out var colour) ? colour.Name : null, index + 1),
                AltText.Validate(image.AltText)))
            .ToList();
    }
}

/// <summary>
/// Adds an image to a product, or to one colour of it (MED-7, MED-9, MED-10, Q-MED-5). The type comes from the file's own bytes, never
/// from its name or declared type; the stored key is generated; the original is kept with its dimensions. Alt text starts empty (a
/// draft may stay without it; a published product needs it at once, MED-5) and the answer carries a suggestion.
/// </summary>
public sealed record UploadProductImageCommand(long ProductId, byte[] Content, string FileName, short? ColourId, string? AltText, string ActingPerson)
    : IRequest<ProductImageDto>, ICatalogCommand;

public sealed class UploadProductImageCommandValidator : AbstractValidator<UploadProductImageCommand>
{
    public UploadProductImageCommandValidator()
    {
        RuleFor(c => c.Content).NotNull().Must(b => b is { Length: > 0 }).WithMessage("Choose an image file.");
        RuleFor(c => c.FileName).NotEmpty().MaximumLength(260);
        RuleFor(c => c.AltText).MaximumLength(AltText.MaxLength * 2);
        RuleFor(c => c.ActingPerson).ActingPerson();
    }
}

public sealed class UploadProductImageCommandHandler(
    ICatalogDbContext dbContext, IFileStorage storage, ProductImageMapper mapper, IProductPathBuilder paths, IOptions<CatalogOptions> options)
    : IRequestHandler<UploadProductImageCommand, ProductImageDto>
{
    public async Task<ProductImageDto> Handle(UploadProductImageCommand request, CancellationToken cancellationToken)
    {
        var catalog = options.Value;
        var product = await dbContext.Products.FirstOrDefaultAsync(p => p.Id == request.ProductId, cancellationToken)
            ?? throw new KeyNotFoundException($"Product {request.ProductId} was not found.");
        if (product.Status == ProductStatus.Archived)
        {
            throw new DomainException("An archived product cannot be edited.");
        }

        if (request.Content.Length > catalog.MaxImageBytes)
        {
            throw Invalid($"The image is {request.Content.Length / 1024 / 1024.0:0.#} MB; the limit is {catalog.MaxImageBytes / 1024 / 1024.0:0.#} MB.");
        }

        var info = ImageSniffer.Inspect(request.Content);
        if (info is null || !Allowed(catalog, info.ContentType))
        {
            throw Invalid("The file is not an image we accept. Use a JPEG, PNG or WebP picture.");
        }

        var altProblem = string.IsNullOrWhiteSpace(request.AltText)
            ? (product.Status == ProductStatus.Published ? "A published product's new image needs alt text that describes it." : null)
            : AltText.Validate(request.AltText);
        if (altProblem is not null)
        {
            throw new Romp.BuildingBlocks.Application.ValidationException(new Dictionary<string, string[]> { ["AltText"] = [altProblem] });
        }

        var existing = await dbContext.Images.AsNoTracking().Where(i => i.ProductId == product.Id).Select(i => i.SortSeq).ToListAsync(cancellationToken);
        if (existing.Count >= catalog.MaxImagesPerProduct)
        {
            throw new DomainException($"A product can have at most {catalog.MaxImagesPerProduct} images. Delete one before adding another.");
        }

        if (request.ColourId is { } colourId
            && !await dbContext.Styles.AsNoTracking().Where(s => s.Id == product.StyleId).SelectMany(s => s.Colourways).AnyAsync(c => c.ColourId == colourId, cancellationToken))
        {
            throw Invalid("That colour is not one of this product's colours.");
        }

        // Storage first: a storage failure then leaves no database row. A later database failure leaves an orphan file, which is harmless.
        var key = await storage.SaveAsync(request.Content, cancellationToken);
        var image = new ProductImage(
            product.Id, request.ColourId, key, info.ContentType, FileContent.SanitiseFileName(request.FileName), request.Content.Length,
            info.Width, info.Height, existing.Count == 0 ? 1 : existing.Max() + 1, request.AltText);
        dbContext.Images.Add(image);

        if (product.Status == ProductStatus.Published)
        {
            product.NoteChanged(await paths.PathOfAsync(product.GenderId, product.CategoryId, product.Slug, cancellationToken));
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var all = await dbContext.Images.AsNoTracking().Where(i => i.ProductId == product.Id).ToListAsync(cancellationToken);
        return (await mapper.MapAsync(product.Name, all, cancellationToken)).Single(i => i.Id == image.Id);
    }

    private static bool Allowed(CatalogOptions catalog, string contentType) =>
        catalog.AllowedImageTypes.Any(t => string.Equals("image/" + t, contentType, StringComparison.OrdinalIgnoreCase));

    private static Romp.BuildingBlocks.Application.ValidationException Invalid(string message) =>
        new(new Dictionary<string, string[]> { ["File"] = [message] });
}

/// <summary>Sets an image's alt text (MED-2, MED-3). Clearing it is allowed only while the product is a draft or unpublished.</summary>
public sealed record SetImageAltCommand(long ImageId, string? Text, string ActingPerson) : IRequest<ProductImageDto>, ICatalogCommand;

public sealed class SetImageAltCommandValidator : AbstractValidator<SetImageAltCommand>
{
    public SetImageAltCommandValidator()
    {
        RuleFor(c => c.Text).MaximumLength(AltText.MaxLength * 2);
        RuleFor(c => c.ActingPerson).ActingPerson();
    }
}

public sealed class SetImageAltCommandHandler(ICatalogDbContext dbContext, ProductImageMapper mapper, IProductPathBuilder paths)
    : IRequestHandler<SetImageAltCommand, ProductImageDto>
{
    public async Task<ProductImageDto> Handle(SetImageAltCommand request, CancellationToken cancellationToken)
    {
        var image = await dbContext.Images.FirstOrDefaultAsync(i => i.Id == request.ImageId, cancellationToken)
            ?? throw new KeyNotFoundException($"Image {request.ImageId} was not found.");
        var product = await dbContext.Products.FirstAsync(p => p.Id == image.ProductId, cancellationToken);
        if (product.Status == ProductStatus.Archived)
        {
            throw new DomainException("An archived product cannot be edited.");
        }

        var problem = string.IsNullOrWhiteSpace(request.Text) ? null : AltText.Validate(request.Text);
        if (problem is not null)
        {
            throw new Romp.BuildingBlocks.Application.ValidationException(new Dictionary<string, string[]> { [nameof(request.Text)] = [problem] });
        }

        image.SetAltText(request.Text, allowEmpty: product.Status != ProductStatus.Published);
        if (product.Status == ProductStatus.Published)
        {
            product.NoteChanged(await paths.PathOfAsync(product.GenderId, product.CategoryId, product.Slug, cancellationToken));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        var all = await dbContext.Images.AsNoTracking().Where(i => i.ProductId == product.Id).ToListAsync(cancellationToken);
        return (await mapper.MapAsync(product.Name, all, cancellationToken)).Single(i => i.Id == image.Id);
    }
}

/// <summary>Sets the order of a product's images (MED-8). The ids must be exactly the product's images; the first is the main image.</summary>
public sealed record ReorderImagesCommand(long ProductId, IReadOnlyList<long> ImageIds, string ActingPerson)
    : IRequest<IReadOnlyList<ProductImageDto>>, ICatalogCommand;

public sealed class ReorderImagesCommandValidator : AbstractValidator<ReorderImagesCommand>
{
    public ReorderImagesCommandValidator()
    {
        RuleFor(c => c.ImageIds).NotNull().Must(ids => ids.Count <= 100).WithMessage("Too many images.");
        RuleFor(c => c.ActingPerson).ActingPerson();
    }
}

public sealed class ReorderImagesCommandHandler(ICatalogDbContext dbContext, ProductImageMapper mapper, IProductPathBuilder paths)
    : IRequestHandler<ReorderImagesCommand, IReadOnlyList<ProductImageDto>>
{
    public async Task<IReadOnlyList<ProductImageDto>> Handle(ReorderImagesCommand request, CancellationToken cancellationToken)
    {
        var product = await dbContext.Products.FirstOrDefaultAsync(p => p.Id == request.ProductId, cancellationToken)
            ?? throw new KeyNotFoundException($"Product {request.ProductId} was not found.");
        if (product.Status == ProductStatus.Archived)
        {
            throw new DomainException("An archived product cannot be edited.");
        }

        var images = await dbContext.Images.Where(i => i.ProductId == product.Id).ToListAsync(cancellationToken);
        var wanted = request.ImageIds.ToList();
        if (wanted.Count != images.Count || wanted.Distinct().Count() != wanted.Count || wanted.Any(id => images.All(i => i.Id != id)))
        {
            throw new DomainException("The new order must list exactly this product's images, each once.");
        }

        for (var position = 0; position < wanted.Count; position++)
        {
            images.First(i => i.Id == wanted[position]).MoveTo(position + 1);
        }

        if (product.Status == ProductStatus.Published)
        {
            product.NoteChanged(await paths.PathOfAsync(product.GenderId, product.CategoryId, product.Slug, cancellationToken));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return await mapper.MapAsync(product.Name, images, cancellationToken);
    }
}

/// <summary>Deletes an image. Refused when it would leave a published product without one. The file is removed after the commit.</summary>
public sealed record DeleteImageCommand(long ImageId, string ActingPerson) : IRequest, ICatalogCommand;

public sealed class DeleteImageCommandValidator : AbstractValidator<DeleteImageCommand>
{
    public DeleteImageCommandValidator() => RuleFor(c => c.ActingPerson).ActingPerson();
}

public sealed class DeleteImageCommandHandler(ICatalogDbContext dbContext, PendingFileDeletes pending, IProductPathBuilder paths)
    : IRequestHandler<DeleteImageCommand>
{
    public async Task Handle(DeleteImageCommand request, CancellationToken cancellationToken)
    {
        var image = await dbContext.Images.FirstOrDefaultAsync(i => i.Id == request.ImageId, cancellationToken)
            ?? throw new KeyNotFoundException($"Image {request.ImageId} was not found.");
        var product = await dbContext.Products.FirstAsync(p => p.Id == image.ProductId, cancellationToken);
        if (product.Status == ProductStatus.Archived)
        {
            throw new DomainException("An archived product cannot be edited.");
        }

        if (product.Status == ProductStatus.Published
            && await dbContext.Images.CountAsync(i => i.ProductId == product.Id, cancellationToken) <= 1)
        {
            throw new DomainException("A published product needs at least one image. Add another image first, or unpublish the product.");
        }

        dbContext.Images.Remove(image);
        pending.Add(image.FileKey);
        if (product.Status == ProductStatus.Published)
        {
            product.NoteChanged(await paths.PathOfAsync(product.GenderId, product.CategoryId, product.Slug, cancellationToken));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>A product's images in display order (MED-4, MED-8).</summary>
public sealed record ListProductImagesQuery(long ProductId) : IRequest<IReadOnlyList<ProductImageDto>>;

public sealed class ListProductImagesQueryHandler(ICatalogDbContext dbContext, ProductImageMapper mapper)
    : IRequestHandler<ListProductImagesQuery, IReadOnlyList<ProductImageDto>>
{
    public async Task<IReadOnlyList<ProductImageDto>> Handle(ListProductImagesQuery request, CancellationToken cancellationToken)
    {
        var name = await dbContext.Products.AsNoTracking().Where(p => p.Id == request.ProductId).Select(p => p.Name).FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException($"Product {request.ProductId} was not found.");
        var images = await dbContext.Images.AsNoTracking().Where(i => i.ProductId == request.ProductId).ToListAsync(cancellationToken);
        return await mapper.MapAsync(name, images, cancellationToken);
    }
}

/// <summary>The stored original of an image with our own record of its type (MED-9): never the client's declared type.</summary>
public sealed record ImageFile(byte[] Content, string ContentType, string FileName);

public sealed record GetImageFileQuery(long ImageId) : IRequest<ImageFile>;

public sealed class GetImageFileQueryHandler(ICatalogDbContext dbContext, IFileStorage storage) : IRequestHandler<GetImageFileQuery, ImageFile>
{
    public async Task<ImageFile> Handle(GetImageFileQuery request, CancellationToken cancellationToken)
    {
        var image = await dbContext.Images.AsNoTracking().Where(i => i.Id == request.ImageId)
            .Select(i => new { i.FileKey, i.ContentType, i.OriginalFileName })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException($"Image {request.ImageId} was not found.");
        return new ImageFile(await storage.RetrieveAsync(image.FileKey, cancellationToken), image.ContentType, image.OriginalFileName);
    }
}
