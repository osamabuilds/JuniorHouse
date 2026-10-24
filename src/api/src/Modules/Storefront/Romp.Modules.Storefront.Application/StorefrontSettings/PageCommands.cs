using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Application;
using Romp.BuildingBlocks.Application.Paging;
using Romp.Modules.Storefront.Application.Abstractions;
using Romp.Modules.Storefront.Domain.StorefrontSettings;

namespace Romp.Modules.Storefront.Application.StorefrontSettings;

/// <summary>Creates a page as a draft (ST-8). The slug is checked against the pattern, the reserved routes and the other pages (ST-9).</summary>
public sealed record CreatePageCommand(string Slug, string Title, string MetaDescription, IReadOnlyList<PageBlockDto> Blocks)
    : IRequest<PageDto>, IStorefrontCommand;

/// <summary>
/// Changes a page's title, description, slug and blocks. The caller quotes the row version it loaded; a stale one is refused (ST-14).
/// Changing the slug of a page that was published writes a 301 from the old slug (ST-9).
/// </summary>
public sealed record UpdatePageCommand(long Id, uint RowVersion, string Slug, string Title, string MetaDescription, IReadOnlyList<PageBlockDto> Blocks)
    : IRequest<PageDto>, IStorefrontCommand;

/// <summary>Moves a page between Draft, Published and Archived. Publishing a page whose title, description or quoted facts are incomplete is refused (ST-17).</summary>
public sealed record ChangePageStatusCommand(long Id, uint RowVersion, PageStatus Status) : IRequest<PageDto>, IStorefrontCommand;

public sealed record GetPageQuery(long Id) : IRequest<PageDto?>;

public sealed record ListPagesQuery(PageStatus? Status = null, int Page = 1, int PageSize = PageRequest.DefaultPageSize)
    : IRequest<PagedResult<PageRowDto>>;

public sealed class CreatePageCommandValidator : AbstractValidator<CreatePageCommand>
{
    public CreatePageCommandValidator()
    {
        RuleFor(c => c.Slug).NotNull();
        RuleFor(c => c.Blocks).NotNull();
    }
}

public sealed class UpdatePageCommandValidator : AbstractValidator<UpdatePageCommand>
{
    public UpdatePageCommandValidator()
    {
        RuleFor(c => c.Slug).NotNull();
        RuleFor(c => c.Blocks).NotNull();
    }
}

internal static class PageSupport
{
    /// <summary>Problems of the one page, plus a slug that is already an old address of another page (it would answer two ways).</summary>
    public static async Task CheckPageAsync(
        IStorefrontDbContext db, SettingsContent content, Page page, long ownRedirectPageId, CancellationToken cancellationToken)
    {
        var prefix = $"pages.{page.Slug}";
        var problems = SettingsValidator.Validate(content)
            .Where(p => p.Field == prefix)
            .ToList();

        var clash = await db.PageRedirects.AnyAsync(r => r.OldSlug == page.Slug && r.PageId != ownRedirectPageId, cancellationToken);
        if (clash)
        {
            problems.Add(new SettingsProblem(prefix, $"\"{page.Slug}\" is an old address of another page. Choose a different page address."));
        }

        DraftSupport.ThrowIfAny(problems);
    }
}

public sealed class CreatePageCommandHandler(IStorefrontDbContext db, IExternalHostPolicy hosts) : IRequestHandler<CreatePageCommand, PageDto>
{
    public async Task<PageDto> Handle(CreatePageCommand request, CancellationToken cancellationToken)
    {
        var loaded = await DraftSupport.LoadAsync(db, hosts, cancellationToken);
        var page = new Page(request.Slug, request.Title, request.MetaDescription);
        page.ReplaceBlocks(request.Blocks.Select(SettingsMapper.ToEntity));

        await PageSupport.CheckPageAsync(db, loaded.Content with { Pages = [.. loaded.Content.Pages, page] }, page, 0, cancellationToken);

        db.Pages.Add(page);
        await db.SaveChangesAsync(cancellationToken);
        return SettingsMapper.ToDto(page, db.RowVersionOf(page));
    }
}

public sealed class UpdatePageCommandHandler(IStorefrontDbContext db, IExternalHostPolicy hosts) : IRequestHandler<UpdatePageCommand, PageDto>
{
    public async Task<PageDto> Handle(UpdatePageCommand request, CancellationToken cancellationToken)
    {
        var loaded = await DraftSupport.LoadAsync(db, hosts, cancellationToken);
        var page = loaded.Content.Pages.SingleOrDefault(p => p.Id == request.Id) ?? throw new KeyNotFoundException($"Page {request.Id} was not found.");

        db.RequireRowVersion(page, request.RowVersion);
        var oldSlug = page.Slug;
        var wasLive = page.Status != PageStatus.Draft;

        var problem = request.Slug.Trim() == oldSlug ? null : Page.CheckSlug(request.Slug);
        if (problem is not null)
        {
            DraftSupport.ThrowIfAny([new SettingsProblem($"pages.{request.Slug.Trim()}", problem)]);
        }

        if (request.Slug.Trim() != oldSlug)
        {
            page.ChangeSlug(request.Slug);
        }

        page.Rename(request.Title, request.MetaDescription);
        page.ReplaceBlocks(request.Blocks.Select(SettingsMapper.ToEntity));

        await PageSupport.CheckPageAsync(db, loaded.Content, page, page.Id, cancellationToken);

        if (page.Slug != oldSlug)
        {
            // The old address of a page that was ever live answers with a 301 to the new one (ST-9). A redirect that already pointed
            // at the new slug from this page is no longer needed.
            var stale = await db.PageRedirects.Where(r => r.OldSlug == page.Slug && r.PageId == page.Id).ToListAsync(cancellationToken);
            db.PageRedirects.RemoveRange(stale);
            if (wasLive)
            {
                db.PageRedirects.Add(new PageRedirect(oldSlug, page.Id));
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return SettingsMapper.ToDto(page, db.RowVersionOf(page));
    }
}

public sealed class ChangePageStatusCommandHandler(IStorefrontDbContext db, IExternalHostPolicy hosts) : IRequestHandler<ChangePageStatusCommand, PageDto>
{
    public async Task<PageDto> Handle(ChangePageStatusCommand request, CancellationToken cancellationToken)
    {
        var loaded = await DraftSupport.LoadAsync(db, hosts, cancellationToken);
        var page = loaded.Content.Pages.SingleOrDefault(p => p.Id == request.Id) ?? throw new KeyNotFoundException($"Page {request.Id} was not found.");

        db.RequireRowVersion(page, request.RowVersion);
        page.SetStatus(request.Status);

        if (request.Status == PageStatus.Published)
        {
            // A page that is not complete (title, description, every quoted fact filled) cannot go live (ST-17).
            DraftSupport.ThrowIfAny([.. SettingsValidator.Validate(loaded.Content).Where(p => p.Field == $"pages.{page.Slug}")]);
        }

        await db.SaveChangesAsync(cancellationToken);
        return SettingsMapper.ToDto(page, db.RowVersionOf(page));
    }
}

public sealed class GetPageQueryHandler(IStorefrontDbContext db) : IRequestHandler<GetPageQuery, PageDto?>
{
    public async Task<PageDto?> Handle(GetPageQuery request, CancellationToken cancellationToken)
    {
        var page = await db.Pages.Include(p => p.Blocks).ThenInclude(b => b.Items).AsSplitQuery()
            .SingleOrDefaultAsync(p => p.Id == request.Id, cancellationToken);
        return page is null ? null : SettingsMapper.ToDto(page, db.RowVersionOf(page));
    }
}

public sealed class ListPagesQueryHandler(IStorefrontDbContext db) : IRequestHandler<ListPagesQuery, PagedResult<PageRowDto>>
{
    public Task<PagedResult<PageRowDto>> Handle(ListPagesQuery request, CancellationToken cancellationToken)
    {
        var query = db.Pages.AsNoTracking().AsQueryable();
        if (request.Status is { } status)
        {
            query = query.Where(p => p.Status == status);
        }

        // Rows are projected in SQL (block count is a subquery), ordered by the unique slug.
        return query
            .OrderBy(p => p.Slug)
            .Select(p => new PageRowDto(p.Id, p.Slug, p.Title, p.Status, p.Blocks.Count))
            .ToPagedResultAsync(new PageRequest(request.Page, request.PageSize), cancellationToken);
    }
}
