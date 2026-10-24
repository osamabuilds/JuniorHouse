using System.Text.Json.Nodes;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Application;
using Romp.BuildingBlocks.Application.Paging;
using Romp.BuildingBlocks.Domain;
using Romp.Modules.Storefront.Application.Abstractions;
using Romp.Modules.Storefront.Domain.StorefrontSettings;

namespace Romp.Modules.Storefront.Application.StorefrontSettings;

/// <summary>
/// Validates the whole draft (no dead links, no empty menu, no empty fact on a published page, size limit), then writes the next
/// snapshot and its event in one transaction and makes it current (ST-3, ST-17). The same content as the current snapshot changes
/// nothing and returns that snapshot. <see cref="ExpectedVersion"/> must be the draft version the admin looked at (ST-14).
/// </summary>
public sealed record PublishSettingsCommand(int ExpectedVersion, string? Note) : IRequest<PublishResultDto>, IStorefrontCommand;

/// <summary>Makes an earlier snapshot current again by publishing a copy of it as a new version; the old row is never edited (ST-12).</summary>
public sealed record RestoreSnapshotCommand(int VersionNo, string? Note) : IRequest<PublishResultDto>, IStorefrontCommand;

public sealed record ListSnapshotsQuery(int Page = 1, int PageSize = PageRequest.DefaultPageSize) : IRequest<PagedResult<SnapshotRowDto>>;

public sealed record GetDraftSettingsQuery : IRequest<DraftSettingsDto>;

/// <summary>The draft as the public document, drafts of pages included, for the admin's preview (never cached, never public).</summary>
public sealed record PreviewSettingsQuery : IRequest<string>;

public sealed class PublishSettingsCommandValidator : AbstractValidator<PublishSettingsCommand>
{
    public PublishSettingsCommandValidator() => RuleFor(c => c.Note).MaximumLength(500);
}

public sealed class RestoreSnapshotCommandValidator : AbstractValidator<RestoreSnapshotCommand>
{
    public RestoreSnapshotCommandValidator()
    {
        RuleFor(c => c.VersionNo).GreaterThan(0);
        RuleFor(c => c.Note).MaximumLength(500);
    }
}

public sealed class ListSnapshotsQueryValidator : AbstractValidator<ListSnapshotsQuery>
{
    public ListSnapshotsQueryValidator() => RuleFor(q => q.Page).GreaterThan(0);
}

internal static class SnapshotSupport
{
    /// <summary>Retires the current snapshot (saved first: only one row may be current) and adds the new one as current.</summary>
    public static async Task<PublishedSnapshot> MakeCurrentAsync(
        IStorefrontDbContext db, int versionNo, string document, string note, CancellationToken cancellationToken)
    {
        foreach (var current in await db.Snapshots.Where(s => s.IsCurrent).ToListAsync(cancellationToken))
        {
            current.Retire();
        }

        await db.SaveChangesAsync(cancellationToken);

        var snapshot = PublishedSnapshot.Create(versionNo, document, note);
        snapshot.MarkPublished();
        db.Snapshots.Add(snapshot);
        await db.SaveChangesAsync(cancellationToken);
        return snapshot;
    }

    public static async Task<int> NextVersionAsync(IStorefrontDbContext db, CancellationToken cancellationToken) =>
        (await db.Snapshots.MaxAsync(s => (int?)s.VersionNo, cancellationToken) ?? 0) + 1;
}

public sealed class PublishSettingsCommandHandler(IStorefrontDbContext db, IExternalHostPolicy hosts)
    : IRequestHandler<PublishSettingsCommand, PublishResultDto>
{
    public async Task<PublishResultDto> Handle(PublishSettingsCommand request, CancellationToken cancellationToken)
    {
        var loaded = await DraftSupport.LoadAsync(db, hosts, cancellationToken);
        if (loaded.Draft.Version != request.ExpectedVersion)
        {
            throw new DomainException("Someone else changed the settings since you opened them. Reload and check them again before publishing.");
        }

        // The whole draft, strictly: every link target must be a published page.
        var content = loaded.Content with { PagesMustBePublished = true };
        DraftSupport.ThrowIfAny(SettingsValidator.Validate(content));

        var redirects = await db.PageRedirects.AsNoTracking().Select(r => new RedirectRow(r.OldSlug, r.PageId)).ToListAsync(cancellationToken);
        var current = await db.Snapshots.AsNoTracking().Where(s => s.IsCurrent).FirstOrDefaultAsync(cancellationToken);

        // Compare at the current version number so only real changes differ; publish the next number if they do.
        if (current is not null && PublishedSnapshot.HashOf(SiteDocumentBuilder.Build(content, current.VersionNo, redirects)) == current.DocumentHash)
        {
            return new PublishResultDto(current.VersionNo, current.DocumentHash, false);
        }

        var versionNo = await SnapshotSupport.NextVersionAsync(db, cancellationToken);
        var document = SiteDocumentBuilder.Build(content, versionNo, redirects);
        var snapshot = await SnapshotSupport.MakeCurrentAsync(db, versionNo, document, request.Note ?? string.Empty, cancellationToken);
        return new PublishResultDto(snapshot.VersionNo, snapshot.DocumentHash, true);
    }
}

public sealed class RestoreSnapshotCommandHandler(IStorefrontDbContext db) : IRequestHandler<RestoreSnapshotCommand, PublishResultDto>
{
    public async Task<PublishResultDto> Handle(RestoreSnapshotCommand request, CancellationToken cancellationToken)
    {
        var source = await db.Snapshots.AsNoTracking().SingleOrDefaultAsync(s => s.VersionNo == request.VersionNo, cancellationToken)
            ?? throw new KeyNotFoundException($"Version {request.VersionNo} was not found.");

        if (source.IsCurrent)
        {
            return new PublishResultDto(source.VersionNo, source.DocumentHash, false);
        }

        var versionNo = await SnapshotSupport.NextVersionAsync(db, cancellationToken);
        var document = JsonNode.Parse(source.Document)!;
        document["settings"]!["version"] = versionNo;

        var note = $"Restored version {source.VersionNo}. {request.Note}".Trim();
        var snapshot = await SnapshotSupport.MakeCurrentAsync(db, versionNo, document.ToJsonString(), note, cancellationToken);
        return new PublishResultDto(snapshot.VersionNo, snapshot.DocumentHash, true);
    }
}

public sealed class ListSnapshotsQueryHandler(IStorefrontDbContext db) : IRequestHandler<ListSnapshotsQuery, PagedResult<SnapshotRowDto>>
{
    public Task<PagedResult<SnapshotRowDto>> Handle(ListSnapshotsQuery request, CancellationToken cancellationToken) =>
        db.Snapshots.AsNoTracking()
            .OrderByDescending(s => s.VersionNo)
            .Select(s => new SnapshotRowDto(s.VersionNo, s.DocumentHash, s.Note, s.InsrDte, s.InsrBy, s.IsCurrent))
            .ToPagedResultAsync(new PageRequest(request.Page, request.PageSize), cancellationToken);
}

public sealed class GetDraftSettingsQueryHandler(IStorefrontDbContext db, IExternalHostPolicy hosts) : IRequestHandler<GetDraftSettingsQuery, DraftSettingsDto>
{
    public async Task<DraftSettingsDto> Handle(GetDraftSettingsQuery request, CancellationToken cancellationToken)
    {
        var loaded = await DraftSupport.LoadAsync(db, hosts, cancellationToken);
        var content = loaded.Content;
        return new DraftSettingsDto(
            loaded.Draft.Version,
            [.. content.Navigation.Select(SettingsMapper.ToDto)],
            [.. content.AgeSteps.Select(SettingsMapper.ToDto)],
            [.. content.Footer.Select(SettingsMapper.ToDto)],
            [.. content.Items.Select(i => new SettingItemDto(i.Key, i.Value))],
            content.PopularSearches,
            [.. content.DeliveryRules.Select(SettingsMapper.ToDto)],
            content.Exchange is null ? null : SettingsMapper.ToDto(content.Exchange),
            [.. content.Pages.OrderBy(p => p.Slug, StringComparer.Ordinal).Select(p => new PageRowDto(p.Id, p.Slug, p.Title, p.Status, p.Blocks.Count))]);
    }
}

public sealed class PreviewSettingsQueryHandler(IStorefrontDbContext db, IExternalHostPolicy hosts) : IRequestHandler<PreviewSettingsQuery, string>
{
    public async Task<string> Handle(PreviewSettingsQuery request, CancellationToken cancellationToken)
    {
        var loaded = await DraftSupport.LoadAsync(db, hosts, cancellationToken);
        var redirects = await db.PageRedirects.AsNoTracking().Select(r => new RedirectRow(r.OldSlug, r.PageId)).ToListAsync(cancellationToken);
        return SiteDocumentBuilder.Build(loaded.Content, 0, redirects, includeDraftPages: true);
    }
}
