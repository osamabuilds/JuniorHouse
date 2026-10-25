using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Application.Paging;
using Romp.BuildingBlocks.Domain;
using Romp.Modules.Catalog.Application.Abstractions;
using Romp.Modules.Catalog.Domain.SizeCharts;
using Romp.Modules.Reference.Contracts;

namespace Romp.Modules.Catalog.Application.SizeCharts;

/// <summary>A row of a chart as typed by staff: a size with its age range (months), height (cm), weight (kg) and optional chest, waist, length (cm).</summary>
public sealed record SizeChartLineInput(
    short SizeId,
    int? AgeMinMonths,
    int? AgeMaxMonths,
    decimal? HeightMinCm,
    decimal? HeightMaxCm,
    decimal? WeightMinKg,
    decimal? WeightMaxKg,
    decimal? ChestCm,
    decimal? WaistCm,
    decimal? LengthCm);

public sealed record SizeChartLineDto(
    short SizeId,
    string SizeName,
    int? AgeMinMonths,
    int? AgeMaxMonths,
    decimal? HeightMinCm,
    decimal? HeightMaxCm,
    decimal? WeightMinKg,
    decimal? WeightMaxKg,
    decimal? ChestCm,
    decimal? WaistCm,
    decimal? LengthCm);

/// <summary>A chart with its rows as structured data (CHT-6): a size label, ranges and a unit (always centimetres and kilograms, Q-CHT-3).</summary>
public sealed record SizeChartDto(long Id, short CategoryId, short GenderId, string Name, string Unit, IReadOnlyList<SizeChartLineDto> Lines, uint Version);

public sealed record SizeChartRowDto(long Id, short CategoryId, short GenderId, string Name, int LineCount);

/// <summary>Builds chart DTOs with size names from one lookup call.</summary>
public sealed class SizeChartMapper(ICatalogLookupQueries lookups)
{
    public const string Unit = "cm";

    public async Task<IReadOnlyList<SizeChartLineDto>> LinesAsync(IEnumerable<SizeChartLine> lines, CancellationToken cancellationToken)
    {
        var list = lines.OrderBy(l => l.SizeId).ToList();
        var sizes = await lookups.GetSizesAsync(list.Select(l => l.SizeId).Distinct().ToList(), cancellationToken);
        return list.Select(l => new SizeChartLineDto(
                l.SizeId, sizes.TryGetValue(l.SizeId, out var s) ? s.Name : string.Empty, l.AgeMinMonths, l.AgeMaxMonths, l.HeightMinCm, l.HeightMaxCm,
                l.WeightMinKg, l.WeightMaxKg, l.ChestCm, l.WaistCm, l.LengthCm))
            .ToList();
    }

    public async Task<SizeChartDto> ToDtoAsync(SizeChart chart, uint version, CancellationToken cancellationToken) =>
        new(chart.Id, chart.CategoryId, chart.GenderId, chart.Name, Unit, await LinesAsync(chart.Lines, cancellationToken), version);
}

internal static class SizeChartRules
{
    public static string? CheckLine(SizeChartLineInput l) =>
        SizeChartLine.Check(l.AgeMinMonths, l.AgeMaxMonths, l.HeightMinCm, l.HeightMaxCm, l.WeightMinKg, l.WeightMaxKg, l.ChestCm, l.WaistCm, l.LengthCm);

    public static SizeChartLine ToLine(SizeChartLineInput l) =>
        new(l.SizeId, l.AgeMinMonths, l.AgeMaxMonths, l.HeightMinCm, l.HeightMaxCm, l.WeightMinKg, l.WeightMaxKg, l.ChestCm, l.WaistCm, l.LengthCm);

    public static async Task EnsureKnownAsync(
        ICatalogLookupQueries lookups, short categoryId, short genderId, IReadOnlyCollection<short> sizeIds, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (!(await lookups.GetCategoriesAsync([categoryId], cancellationToken)).ContainsKey(categoryId))
        {
            errors["CategoryId"] = ["Choose a category from the list."];
        }

        if (!(await lookups.GetGendersAsync([genderId], cancellationToken)).ContainsKey(genderId))
        {
            errors["GenderId"] = ["Choose a gender from the list."];
        }

        var sizes = await lookups.GetSizesAsync(sizeIds, cancellationToken);
        if (sizeIds.Any(id => !sizes.ContainsKey(id)))
        {
            errors["Lines"] = ["Choose sizes from the list."];
        }

        if (errors.Count > 0)
        {
            throw new Romp.BuildingBlocks.Application.ValidationException(errors);
        }
    }
}

internal static class SizeChartValidationExtensions
{
    /// <summary>The rules for a chart's rows (CHT-4, CHT-5): at least one, not too many, each row valid, one row per size.</summary>
    public static void LineRules<T>(this AbstractValidator<T> validator, System.Linq.Expressions.Expression<Func<T, IEnumerable<SizeChartLineInput>>> lines)
    {
        validator.RuleFor(lines).NotEmpty().WithMessage("Add at least one size.").Must(l => l.Count() <= 60).WithMessage("Too many rows.");
        validator.RuleForEach(lines).Must(l => SizeChartRules.CheckLine(l) is null).WithMessage((_, l) => SizeChartRules.CheckLine(l) ?? string.Empty);
        validator.RuleFor(lines).Must(l => l is null || l.Select(x => x.SizeId).Distinct().Count() == l.Count()).WithMessage("Each size can appear only once in a chart.");
    }
}

/// <summary>Creates the chart for a category and gender (CHT-4, CHT-5). Unisex is the fallback chart for any gender without its own. Writes the changed event.</summary>
public sealed record CreateSizeChartCommand(short CategoryId, short GenderId, string Name, IReadOnlyList<SizeChartLineInput> Lines, string ActingPerson)
    : IRequest<SizeChartDto>, ICatalogCommand;

public sealed class CreateSizeChartCommandValidator : AbstractValidator<CreateSizeChartCommand>
{
    public CreateSizeChartCommandValidator()
    {
        RuleFor(c => c.Name).NotEmpty().MaximumLength(100);
        RuleFor(c => c.CategoryId).GreaterThan((short)0);
        RuleFor(c => c.GenderId).GreaterThan((short)0);
        this.LineRules(c => c.Lines);
        RuleFor(c => c.ActingPerson).ActingPerson();
    }
}

public sealed class CreateSizeChartCommandHandler(ICatalogDbContext dbContext, ICatalogLookupQueries lookups, SizeChartMapper mapper)
    : IRequestHandler<CreateSizeChartCommand, SizeChartDto>
{
    public async Task<SizeChartDto> Handle(CreateSizeChartCommand request, CancellationToken cancellationToken)
    {
        await SizeChartRules.EnsureKnownAsync(lookups, request.CategoryId, request.GenderId, request.Lines.Select(l => l.SizeId).ToList(), cancellationToken);
        if (await dbContext.Charts.AnyAsync(c => c.CategoryId == request.CategoryId && c.GenderId == request.GenderId, cancellationToken))
        {
            throw new DomainException("There is already a size chart for that category and gender. Edit it instead.");
        }

        var chart = new SizeChart(request.CategoryId, request.GenderId, request.Name, request.Lines.Select(SizeChartRules.ToLine));
        dbContext.Charts.Add(chart);
        await dbContext.SaveChangesAsync(cancellationToken);

        chart.NoteChanged();
        await dbContext.SaveChangesAsync(cancellationToken);
        return await mapper.ToDtoAsync(chart, dbContext.VersionOf(chart), cancellationToken);
    }
}

/// <summary>Replaces a chart's name and rows (CHT-2). A stale <c>Version</c> is a 409. The changed event is written in the same transaction.</summary>
public sealed record UpdateSizeChartCommand(long ChartId, string Name, IReadOnlyList<SizeChartLineInput> Lines, uint Version, string ActingPerson)
    : IRequest<SizeChartDto>, ICatalogCommand;

public sealed class UpdateSizeChartCommandValidator : AbstractValidator<UpdateSizeChartCommand>
{
    public UpdateSizeChartCommandValidator()
    {
        RuleFor(c => c.Name).NotEmpty().MaximumLength(100);
        this.LineRules(c => c.Lines);
        RuleFor(c => c.ActingPerson).ActingPerson();
    }
}

public sealed class UpdateSizeChartCommandHandler(ICatalogDbContext dbContext, ICatalogLookupQueries lookups, SizeChartMapper mapper)
    : IRequestHandler<UpdateSizeChartCommand, SizeChartDto>
{
    public async Task<SizeChartDto> Handle(UpdateSizeChartCommand request, CancellationToken cancellationToken)
    {
        var chart = await dbContext.Charts.Include(c => c.Lines).FirstOrDefaultAsync(c => c.Id == request.ChartId, cancellationToken)
            ?? throw new KeyNotFoundException($"Size chart {request.ChartId} was not found.");
        dbContext.ExpectVersion(chart, request.Version);
        await SizeChartRules.EnsureKnownAsync(lookups, chart.CategoryId, chart.GenderId, request.Lines.Select(l => l.SizeId).ToList(), cancellationToken);

        chart.Update(request.Name, request.Lines.Select(SizeChartRules.ToLine));
        chart.Stamp(await dbContext.NextChartVersionAsync(cancellationToken));
        dbContext.Touch(chart);
        chart.NoteChanged();
        await dbContext.SaveChangesAsync(cancellationToken);
        return await mapper.ToDtoAsync(chart, dbContext.VersionOf(chart), cancellationToken);
    }
}

/// <summary>Deletes a chart. Allowed: products of that category then report "no size chart" (CHT-3). Writes the changed event.</summary>
public sealed record DeleteSizeChartCommand(long ChartId, string ActingPerson) : IRequest, ICatalogCommand;

public sealed class DeleteSizeChartCommandValidator : AbstractValidator<DeleteSizeChartCommand>
{
    public DeleteSizeChartCommandValidator() => RuleFor(c => c.ActingPerson).ActingPerson();
}

public sealed class DeleteSizeChartCommandHandler(ICatalogDbContext dbContext) : IRequestHandler<DeleteSizeChartCommand>
{
    public async Task Handle(DeleteSizeChartCommand request, CancellationToken cancellationToken)
    {
        var chart = await dbContext.Charts.Include(c => c.Lines).FirstOrDefaultAsync(c => c.Id == request.ChartId, cancellationToken)
            ?? throw new KeyNotFoundException($"Size chart {request.ChartId} was not found.");
        chart.NoteChanged();
        dbContext.Charts.Remove(chart);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>The paged chart list (CHT-8): ordered by category then gender then id; one COUNT and one page query.</summary>
public sealed record ListSizeChartsQuery(int Page = 1, int PageSize = PageRequest.DefaultPageSize) : IRequest<PagedResult<SizeChartRowDto>>;

public sealed class ListSizeChartsQueryHandler(ICatalogDbContext dbContext) : IRequestHandler<ListSizeChartsQuery, PagedResult<SizeChartRowDto>>
{
    public Task<PagedResult<SizeChartRowDto>> Handle(ListSizeChartsQuery request, CancellationToken cancellationToken) =>
        dbContext.Charts.AsNoTracking()
            .OrderBy(c => c.CategoryId).ThenBy(c => c.GenderId).ThenBy(c => c.Id)
            .Select(c => new SizeChartRowDto(c.Id, c.CategoryId, c.GenderId, c.Name, c.Lines.Count))
            .ToPagedResultAsync(new PageRequest(request.Page, request.PageSize), cancellationToken);
}

public sealed record GetSizeChartQuery(long ChartId) : IRequest<SizeChartDto>;

public sealed class GetSizeChartQueryHandler(ICatalogDbContext dbContext, SizeChartMapper mapper) : IRequestHandler<GetSizeChartQuery, SizeChartDto>
{
    public async Task<SizeChartDto> Handle(GetSizeChartQuery request, CancellationToken cancellationToken)
    {
        var chart = await dbContext.Charts.Include(c => c.Lines).FirstOrDefaultAsync(c => c.Id == request.ChartId, cancellationToken)
            ?? throw new KeyNotFoundException($"Size chart {request.ChartId} was not found.");
        return await mapper.ToDtoAsync(chart, dbContext.VersionOf(chart), cancellationToken);
    }
}

/// <summary>
/// The chart that applies to a product (CHT-1, CHT-3): the chart for its category and gender, else the category's Unisex chart, else
/// none. Rows are limited to the sizes of the product's size run, in size order. <c>Found</c> false means "no size chart".
/// </summary>
public sealed record GetChartForProductQuery(long ProductId) : IRequest<ProductSizeChartDto>;

public sealed record ProductSizeChartDto(bool Found, long? ChartId, string? Name, string Unit, IReadOnlyList<SizeChartLineDto> Lines);

public sealed class GetChartForProductQueryHandler(ICatalogDbContext dbContext, ICatalogLookupQueries lookups, SizeChartMapper mapper)
    : IRequestHandler<GetChartForProductQuery, ProductSizeChartDto>
{
    public async Task<ProductSizeChartDto> Handle(GetChartForProductQuery request, CancellationToken cancellationToken)
    {
        var product = await dbContext.Products.AsNoTracking().Where(p => p.Id == request.ProductId)
            .Select(p => new { p.StyleId, p.CategoryId, p.GenderId })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException($"Product {request.ProductId} was not found.");

        var unisex = await lookups.GetGenderIdsBySegmentAsync([ChartGate.UnisexSegment], cancellationToken);
        var fallback = unisex.TryGetValue(ChartGate.UnisexSegment, out var id) ? id : (short)0;

        var candidates = await dbContext.Charts.AsNoTracking().Include(c => c.Lines)
            .Where(c => c.CategoryId == product.CategoryId && (c.GenderId == product.GenderId || c.GenderId == fallback))
            .ToListAsync(cancellationToken);
        var chart = ChartLookupRule.Choose(candidates, product.CategoryId, product.GenderId, fallback, c => (c.CategoryId, c.GenderId));
        if (chart is null)
        {
            return new ProductSizeChartDto(false, null, null, SizeChartMapper.Unit, []);
        }

        var run = await dbContext.Styles.AsNoTracking().Where(s => s.Id == product.StyleId).SelectMany(s => s.Sizes).Select(s => s.SizeId).ToListAsync(cancellationToken);
        var lines = await mapper.LinesAsync(chart.Lines.Where(l => run.Contains(l.SizeId)), cancellationToken);
        return new ProductSizeChartDto(true, chart.Id, chart.Name, SizeChartMapper.Unit, lines);
    }
}
