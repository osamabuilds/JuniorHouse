using System.Text.RegularExpressions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Application;
using Romp.BuildingBlocks.Application.Paging;
using Romp.Modules.Storefront.Application.Abstractions;
using Romp.Modules.Storefront.Domain.Search;

namespace Romp.Modules.Storefront.Application.Search;

// Staff screens for search (SRC-12, 13): the synonym groups the search expands, and the log of searches that found nothing.

/// <summary>A group of terms that mean the same thing. <c>RowVersion</c> is quoted back on update, so a stale save is refused.</summary>
public sealed record SynonymGroupDto(long Id, string Name, bool IsActive, IReadOnlyList<string> Terms, uint RowVersion);

public sealed record MissRowDto(string Text, DateOnly Day, int Count);

public sealed record ListSynonymGroupsQuery(int Page = 1, int PageSize = PageRequest.DefaultPageSize) : IRequest<PagedResult<SynonymGroupDto>>;

public sealed record CreateSynonymGroupCommand(string Name, bool IsActive, IReadOnlyList<string> Terms) : IRequest<SynonymGroupDto>;

public sealed record UpdateSynonymGroupCommand(long Id, uint RowVersion, string Name, bool IsActive, IReadOnlyList<string> Terms) : IRequest<SynonymGroupDto>;

public sealed record DeleteSynonymGroupCommand(long Id) : IRequest<bool>;

/// <summary>Zero-result searches, most frequent first, so staff can see what shoppers look for and add a synonym (SRC-13).</summary>
public sealed record ListMissesQuery(int Page = 1, int PageSize = PageRequest.DefaultPageSize) : IRequest<PagedResult<MissRowDto>>;

internal static partial class SynonymRules
{
    public const int MaxTerms = 20;
    public const int MaxTermLength = 60;
    public const int MaxWords = 3;

    [GeneratedRegex(@"^[\p{L}\p{N}][\p{L}\p{N} \-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex Allowed();

    /// <summary>The normalised terms, or the problems that stop the save. Nothing is stored when any term is refused.</summary>
    public static (List<string> Terms, Dictionary<string, string[]> Errors) Check(string? name, IReadOnlyList<string>? raw)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 60)
        {
            errors["name"] = ["The name is required and at most 60 characters."];
        }

        var terms = new List<string>();
        var problems = new List<string>();
        foreach (var item in raw ?? [])
        {
            var term = SearchQuery.Normalise(item);
            if (term.Length is 0 or > MaxTermLength || !Allowed().IsMatch(term))
            {
                problems.Add($"\"{Shorten(item)}\" is not a usable term: use 1 to {MaxTermLength} letters, digits, spaces or hyphens.");
            }
            else if (term.Split(' ').Length > MaxWords)
            {
                problems.Add($"\"{term}\" has more than {MaxWords} words.");
            }
            else if (terms.Contains(term, StringComparer.Ordinal))
            {
                problems.Add($"\"{term}\" is listed twice.");
            }
            else
            {
                terms.Add(term);
            }
        }

        if (terms.Count is < 2 or > MaxTerms && problems.Count == 0)
        {
            problems.Add($"A group needs 2 to {MaxTerms} terms.");
        }

        if (problems.Count > 0)
        {
            errors["terms"] = [.. problems];
        }

        return (terms, errors);
    }

    private static string Shorten(string? text) => text is null ? string.Empty : text.Length <= 30 ? text : text[..30] + "…";
}

internal sealed record GroupRow(long Id, string Name, bool IsActive, uint RowVersion);

public sealed class ListSynonymGroupsQueryHandler(IStorefrontReadDbContext db) : IRequestHandler<ListSynonymGroupsQuery, PagedResult<SynonymGroupDto>>
{
    public async Task<PagedResult<SynonymGroupDto>> Handle(ListSynonymGroupsQuery request, CancellationToken cancellationToken)
    {
        // The row version is read as a column (xmin): the page is not tracked, so it cannot be asked of an entity.
        var groups = await db.SynonymGroups.AsNoTracking()
            .OrderBy(g => g.Name).ThenBy(g => g.Id)
            .Select(g => new GroupRow(g.Id, g.Name, g.IsActive, EF.Property<uint>(g, "xmin")))
            .ToPagedResultAsync(new PageRequest(request.Page, request.PageSize), cancellationToken);

        // Two queries however many groups: the page, then every term of the page's groups.
        var ids = groups.Items.Select(g => g.Id).ToList();
        var terms = await db.SynonymTerms.AsNoTracking().Where(t => ids.Contains(t.GroupId)).OrderBy(t => t.Term).ToListAsync(cancellationToken);
        var byGroup = terms.ToLookup(t => t.GroupId, t => t.Term);

        return new PagedResult<SynonymGroupDto>(
            [.. groups.Items.Select(g => new SynonymGroupDto(g.Id, g.Name, g.IsActive, [.. byGroup[g.Id]], g.RowVersion))],
            groups.Page, groups.PageSize, groups.TotalCount);
    }
}

public sealed class CreateSynonymGroupCommandHandler(IStorefrontReadDbContext db, ISynonymCache cache) : IRequestHandler<CreateSynonymGroupCommand, SynonymGroupDto>
{
    public async Task<SynonymGroupDto> Handle(CreateSynonymGroupCommand request, CancellationToken cancellationToken)
    {
        var (terms, errors) = SynonymRules.Check(request.Name, request.Terms);
        await SynonymChecks.AddClashesAsync(db, terms, ownGroupId: 0, errors, cancellationToken);
        await SynonymChecks.AddNameClashAsync(db, request.Name, ownGroupId: 0, errors, cancellationToken);
        SynonymChecks.ThrowIfAny(errors);

        var group = new SynonymGroup(request.Name, request.IsActive);
        db.SynonymGroups.Add(group);
        await db.SaveChangesAsync(cancellationToken);
        db.SynonymTerms.AddRange(terms.Select(t => new SynonymTerm(group.Id, t)));
        await db.SaveChangesAsync(cancellationToken);

        cache.Invalidate();
        return new SynonymGroupDto(group.Id, group.Name, group.IsActive, [.. terms.Order(StringComparer.Ordinal)], db.RowVersionOf(group));
    }
}

public sealed class UpdateSynonymGroupCommandHandler(IStorefrontReadDbContext db, ISynonymCache cache) : IRequestHandler<UpdateSynonymGroupCommand, SynonymGroupDto>
{
    public async Task<SynonymGroupDto> Handle(UpdateSynonymGroupCommand request, CancellationToken cancellationToken)
    {
        var group = await db.SynonymGroups.FirstOrDefaultAsync(g => g.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("That synonym group does not exist.");

        var (terms, errors) = SynonymRules.Check(request.Name, request.Terms);
        await SynonymChecks.AddClashesAsync(db, terms, group.Id, errors, cancellationToken);
        await SynonymChecks.AddNameClashAsync(db, request.Name, group.Id, errors, cancellationToken);
        SynonymChecks.ThrowIfAny(errors);

        group.Rename(request.Name, request.IsActive);
        db.RequireRowVersion(group, request.RowVersion);

        // Keep the terms that stay, drop the ones that went, add the new ones: no delete-and-reinsert of a unique value in one save.
        var existing = await db.SynonymTerms.Where(t => t.GroupId == group.Id).ToListAsync(cancellationToken);
        db.SynonymTerms.RemoveRange(existing.Where(t => !terms.Contains(t.Term, StringComparer.Ordinal)));
        db.SynonymTerms.AddRange(terms.Where(t => !existing.Exists(e => e.Term == t)).Select(t => new SynonymTerm(group.Id, t)));
        await db.SaveChangesAsync(cancellationToken);

        cache.Invalidate();
        return new SynonymGroupDto(group.Id, group.Name, group.IsActive, [.. terms.Order(StringComparer.Ordinal)], db.RowVersionOf(group));
    }
}

public sealed class DeleteSynonymGroupCommandHandler(IStorefrontReadDbContext db, ISynonymCache cache) : IRequestHandler<DeleteSynonymGroupCommand, bool>
{
    public async Task<bool> Handle(DeleteSynonymGroupCommand request, CancellationToken cancellationToken)
    {
        var group = await db.SynonymGroups.FirstOrDefaultAsync(g => g.Id == request.Id, cancellationToken);
        if (group is null)
        {
            return false;
        }

        // The terms go with the group (database cascade).
        db.SynonymGroups.Remove(group);
        await db.SaveChangesAsync(cancellationToken);
        cache.Invalidate();
        return true;
    }
}

public sealed class ListMissesQueryHandler(IStorefrontReadDbContext db) : IRequestHandler<ListMissesQuery, PagedResult<MissRowDto>>
{
    public Task<PagedResult<MissRowDto>> Handle(ListMissesQuery request, CancellationToken cancellationToken) =>
        db.SearchMisses.AsNoTracking()
            .OrderByDescending(m => m.MissCount).ThenByDescending(m => m.MissDate).ThenBy(m => m.QueryText)
            .Select(m => new MissRowDto(m.QueryText, m.MissDate, m.MissCount))
            .ToPagedResultAsync(new PageRequest(request.Page, request.PageSize), cancellationToken);
}

internal static class SynonymChecks
{
    /// <summary>A term may belong to one group only; say which terms are taken so staff can fix the right one.</summary>
    public static async Task AddClashesAsync(
        IStorefrontReadDbContext db, List<string> terms, long ownGroupId, Dictionary<string, string[]> errors, CancellationToken cancellationToken)
    {
        if (terms.Count == 0)
        {
            return;
        }

        var taken = await db.SynonymTerms.AsNoTracking().Where(t => terms.Contains(t.Term) && t.GroupId != ownGroupId).Select(t => t.Term).ToListAsync(cancellationToken);
        if (taken.Count > 0)
        {
            var existing = errors.TryGetValue("terms", out var current) ? current : [];
            errors["terms"] = [.. existing, .. taken.Select(t => $"\"{t}\" already belongs to another group.")];
        }
    }

    public static async Task AddNameClashAsync(
        IStorefrontReadDbContext db, string? name, long ownGroupId, Dictionary<string, string[]> errors, CancellationToken cancellationToken)
    {
        var trimmed = name?.Trim();
        if (!string.IsNullOrEmpty(trimmed) && !errors.ContainsKey("name")
            && await db.SynonymGroups.AsNoTracking().AnyAsync(g => g.Name == trimmed && g.Id != ownGroupId, cancellationToken))
        {
            errors["name"] = ["Another group already has this name."];
        }
    }

    public static void ThrowIfAny(Dictionary<string, string[]> errors)
    {
        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }
    }
}
