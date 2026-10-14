using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.Modules.Catalog.Application.Abstractions;
using Romp.Modules.Catalog.Application.Urls;
using Romp.Modules.Catalog.Domain.CompositionCare;
using Romp.Modules.Catalog.Domain.Products;
using Romp.Modules.Reference.Contracts;

namespace Romp.Modules.Catalog.Application.CompositionCare;

public sealed record CompositionRowInput(short FibreId, decimal Percent);

public sealed record CompositionRowDto(short FibreId, string FibreName, decimal Percent);

public sealed record CareInstructionDto(short Id, string Code, string Name, string SymbolCode);

/// <summary>
/// A product's composition and care as the editor and the storefront need it (CMP-3, CMP-4). <c>Total</c> is exact; <c>IsComplete</c>
/// is true only at exactly 100%. Every care instruction carries its text name as well as its symbol key. <c>Version</c> is the
/// product's concurrency token.
/// </summary>
public sealed record CompositionCareDto(
    long ProductId,
    IReadOnlyList<CompositionRowDto> Rows,
    decimal Total,
    bool IsComplete,
    IReadOnlyList<CareInstructionDto> Care,
    string? CareNote,
    uint Version);

/// <summary>Reads the lookups and builds the combined DTO (a constant number of queries).</summary>
public sealed class CompositionCareReader(ICatalogDbContext dbContext, ICatalogLookupQueries lookups)
{
    public async Task<CompositionCareDto> ReadAsync(Product product, CancellationToken cancellationToken)
    {
        var lines = await dbContext.CompositionLines.AsNoTracking().Where(l => l.ProductId == product.Id).OrderByDescending(l => l.Percent).ThenBy(l => l.Id).ToListAsync(cancellationToken);
        var careIds = await dbContext.CareInstructions.AsNoTracking().Where(c => c.ProductId == product.Id).Select(c => c.CareId).ToListAsync(cancellationToken);
        var fibres = await lookups.GetFibresAsync(lines.Select(l => l.FibreId).Distinct().ToList(), cancellationToken);
        var care = await lookups.GetCareInstructionsAsync(careIds, cancellationToken);

        var rows = lines.Select(l => new CompositionRowDto(l.FibreId, fibres.TryGetValue(l.FibreId, out var f) ? f.Name : string.Empty, l.Percent)).ToList();
        var total = Composition.Total(lines.Select(l => l.Percent));
        return new CompositionCareDto(
            product.Id,
            rows,
            total,
            total == Composition.CompleteTotal,
            care.Values.OrderBy(c => c.Id).Select(c => new CareInstructionDto(c.Id, c.Code, c.Name, c.SymbolCode)).ToList(),
            product.CareNote,
            dbContext.VersionOf(product));
    }
}

/// <summary>
/// Replaces a product's composition (CMP-1, CMP-6, CMP-7). Incomplete totals can be saved while drafting; publishing needs exactly
/// 100%. No fibre twice, each share 0 &lt; p ≤ 100 with at most one decimal. A published product tells the storefront (CMP-5).
/// </summary>
public sealed record SetCompositionCommand(long ProductId, IReadOnlyList<CompositionRowInput> Rows, uint Version, string ActingPerson)
    : IRequest<CompositionCareDto>, ICatalogCommand;

public sealed class SetCompositionCommandValidator : AbstractValidator<SetCompositionCommand>
{
    public SetCompositionCommandValidator()
    {
        RuleFor(c => c.Rows).NotNull().Must(rows => rows.Count <= 50).WithMessage("Too many rows.");
        RuleFor(c => c.Rows)
            .Must(rows => Composition.Check(rows.Select(r => (r.FibreId, r.Percent)).ToList()) is null)
            .WithMessage(c => Composition.Check(c.Rows.Select(r => (r.FibreId, r.Percent)).ToList()) ?? string.Empty)
            .When(c => c.Rows is not null);
        RuleFor(c => c.ActingPerson).ActingPerson();
    }
}

public sealed class SetCompositionCommandHandler(
    ICatalogDbContext dbContext, ICatalogLookupQueries lookups, CompositionCareReader reader, IProductPathBuilder paths)
    : IRequestHandler<SetCompositionCommand, CompositionCareDto>
{
    public async Task<CompositionCareDto> Handle(SetCompositionCommand request, CancellationToken cancellationToken)
    {
        var product = await dbContext.Products.FirstOrDefaultAsync(p => p.Id == request.ProductId, cancellationToken)
            ?? throw new KeyNotFoundException($"Product {request.ProductId} was not found.");
        dbContext.ExpectVersion(product, request.Version);
        if (product.Status == ProductStatus.Archived)
        {
            throw new Romp.BuildingBlocks.Domain.DomainException("An archived product cannot be edited.");
        }

        var wanted = request.Rows.Select(r => r.FibreId).Distinct().ToList();
        var known = await lookups.GetFibresAsync(wanted, cancellationToken);
        if (wanted.Any(id => !known.ContainsKey(id)))
        {
            throw new Romp.BuildingBlocks.Application.ValidationException(new Dictionary<string, string[]> { [nameof(request.Rows)] = ["Choose fibres from the list."] });
        }

        var existing = await dbContext.CompositionLines.Where(l => l.ProductId == product.Id).ToListAsync(cancellationToken);
        dbContext.CompositionLines.RemoveRange(existing);
        dbContext.CompositionLines.AddRange(request.Rows.Select(r => new CompositionLine(product.Id, r.FibreId, r.Percent)));
        dbContext.Touch(product);
        if (product.Status == ProductStatus.Published)
        {
            product.NoteChanged(await paths.PathOfAsync(product.GenderId, product.CategoryId, product.Slug, cancellationToken));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return await reader.ReadAsync(product, cancellationToken);
    }
}

/// <summary>
/// Replaces a product's care instructions and note (CMP-2, CMP-4, CMP-5). The instructions are lookup rows, each with a text name
/// and a symbol key. The note is plain text of at most 500 characters.
/// </summary>
public sealed record SetCareCommand(long ProductId, IReadOnlyList<short> CareIds, string? Note, uint Version, string ActingPerson)
    : IRequest<CompositionCareDto>, ICatalogCommand;

public sealed class SetCareCommandValidator : AbstractValidator<SetCareCommand>
{
    public SetCareCommandValidator()
    {
        RuleFor(c => c.CareIds).NotNull().Must(ids => ids.Count <= 30).WithMessage("Choose at most 30 care instructions.");
        RuleFor(c => c.Note).Must(note => Product.CheckCareNote(note) is null).WithMessage((_, note) => Product.CheckCareNote(note) ?? string.Empty);
        RuleFor(c => c.ActingPerson).ActingPerson();
    }
}

public sealed class SetCareCommandHandler(
    ICatalogDbContext dbContext, ICatalogLookupQueries lookups, CompositionCareReader reader, IProductPathBuilder paths)
    : IRequestHandler<SetCareCommand, CompositionCareDto>
{
    public async Task<CompositionCareDto> Handle(SetCareCommand request, CancellationToken cancellationToken)
    {
        var product = await dbContext.Products.FirstOrDefaultAsync(p => p.Id == request.ProductId, cancellationToken)
            ?? throw new KeyNotFoundException($"Product {request.ProductId} was not found.");
        dbContext.ExpectVersion(product, request.Version);

        var wanted = request.CareIds.Distinct().ToList();
        var known = await lookups.GetCareInstructionsAsync(wanted, cancellationToken);
        if (wanted.Any(id => !known.ContainsKey(id)))
        {
            throw new Romp.BuildingBlocks.Application.ValidationException(new Dictionary<string, string[]> { [nameof(request.CareIds)] = ["Choose care instructions from the list."] });
        }

        var existing = await dbContext.CareInstructions.Where(c => c.ProductId == product.Id).ToListAsync(cancellationToken);
        dbContext.CareInstructions.RemoveRange(existing);
        dbContext.CareInstructions.AddRange(wanted.Select(id => new ProductCare(product.Id, id)));
        product.SetCareNote(request.Note);
        dbContext.Touch(product);
        if (product.Status == ProductStatus.Published)
        {
            product.NoteChanged(await paths.PathOfAsync(product.GenderId, product.CategoryId, product.Slug, cancellationToken));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return await reader.ReadAsync(product, cancellationToken);
    }
}

/// <summary>A product's composition and care (CMP-3, CMP-4).</summary>
public sealed record GetCompositionAndCareQuery(long ProductId) : IRequest<CompositionCareDto>;

public sealed class GetCompositionAndCareQueryHandler(ICatalogDbContext dbContext, CompositionCareReader reader)
    : IRequestHandler<GetCompositionAndCareQuery, CompositionCareDto>
{
    public async Task<CompositionCareDto> Handle(GetCompositionAndCareQuery request, CancellationToken cancellationToken)
    {
        var product = await dbContext.Products.FirstOrDefaultAsync(p => p.Id == request.ProductId, cancellationToken)
            ?? throw new KeyNotFoundException($"Product {request.ProductId} was not found.");
        return await reader.ReadAsync(product, cancellationToken);
    }
}
