using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Application.Paging;
using Romp.Modules.Identity.Application.Abstractions;
using Romp.Modules.Identity.Domain.AuditTrail;

namespace Romp.Modules.Identity.Application.AuditTrail;

/// <summary>One audit entry as the Audit page shows it: which record, what happened, who, when, and the NAMES of the changed columns.</summary>
public sealed record AuditEntryDto(
    long Id,
    DateTimeOffset OccurredAt,
    string Module,
    string EntityType,
    string EntityKey,
    string Operation,
    string Actor,
    string? CorrelationId,
    IReadOnlyList<string> Columns);

/// <summary>
/// The audit list (audit-trail AC-8): newest first, filtered by person, module, record type, record key, operation and date range.
/// With no dates it covers the last <see cref="DefaultDays"/> days, and a range may not exceed <see cref="MaxRangeDays"/> days, so every
/// query prunes partitions and stays index-backed. One COUNT and one page query: the column names ride in the same SELECT.
/// </summary>
public sealed record ListAuditQuery(
    string? Actor = null,
    string? Module = null,
    string? EntityType = null,
    string? EntityKey = null,
    string? Operation = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    int Page = 1,
    int PageSize = PageRequest.DefaultPageSize) : IRequest<PagedResult<AuditEntryDto>>
{
    public const int DefaultDays = 30;
    public const int MaxRangeDays = 366;
    public const int MaxPageSize = 100;
    public const int MaxFilterLength = 200;
}

public sealed class ListAuditQueryValidator : AbstractValidator<ListAuditQuery>
{
    public ListAuditQueryValidator()
    {
        RuleFor(q => q.Page).GreaterThanOrEqualTo(1);
        RuleFor(q => q.PageSize).InclusiveBetween(1, ListAuditQuery.MaxPageSize);
        RuleFor(q => q.Actor).MaximumLength(AuditEntry.ActorMaxLength);
        RuleFor(q => q.Module).MaximumLength(AuditEntry.ModuleMaxLength);
        RuleFor(q => q.EntityType).MaximumLength(AuditEntry.EntityTypeMaxLength);
        RuleFor(q => q.EntityKey).MaximumLength(AuditEntry.EntityKeyMaxLength);
        RuleFor(q => q.Operation).Must(code => code is null or "" || Enum.TryParse<AuditOperation>(code, ignoreCase: true, out _))
            .WithMessage("Operation must be INSERT, UPDATE or DELETE.");
        RuleFor(q => q).Must(q => q.From is null || q.To is null || q.From <= q.To)
            .WithMessage("The start of the range must not be after its end.");
        RuleFor(q => q).Must(q => q.From is null || q.To is null || q.To - q.From <= TimeSpan.FromDays(ListAuditQuery.MaxRangeDays))
            .WithMessage($"A date range may span at most {ListAuditQuery.MaxRangeDays} days.");
    }
}

public sealed class ListAuditQueryHandler(IIdentityDbContext dbContext, TimeProvider time) : IRequestHandler<ListAuditQuery, PagedResult<AuditEntryDto>>
{
    public async Task<PagedResult<AuditEntryDto>> Handle(ListAuditQuery request, CancellationToken cancellationToken)
    {
        var to = request.To ?? time.GetUtcNow();
        var from = request.From ?? to.AddDays(-ListAuditQuery.DefaultDays);
        if (to - from > TimeSpan.FromDays(ListAuditQuery.MaxRangeDays))
        {
            from = to.AddDays(-ListAuditQuery.MaxRangeDays); // only reachable with a single open end; the validator refuses two explicit dates
        }

        var entries = dbContext.AuditEntries.AsNoTracking().Where(e => e.OccurredAt >= from && e.OccurredAt <= to);
        if (!string.IsNullOrWhiteSpace(request.Actor))
        {
            var actor = request.Actor.Trim();
            entries = entries.Where(e => e.Actor == actor);
        }

        if (!string.IsNullOrWhiteSpace(request.Module))
        {
            var module = request.Module.Trim();
            entries = entries.Where(e => e.Module == module);
        }

        if (!string.IsNullOrWhiteSpace(request.EntityType))
        {
            var type = request.EntityType.Trim();
            entries = entries.Where(e => e.EntityType == type);
        }

        if (!string.IsNullOrWhiteSpace(request.EntityKey))
        {
            var key = request.EntityKey.Trim();
            entries = entries.Where(e => e.EntityKey == key);
        }

        if (!string.IsNullOrWhiteSpace(request.Operation))
        {
            var operation = Enum.Parse<AuditOperation>(request.Operation, ignoreCase: true);
            entries = entries.Where(e => e.Operation == operation);
        }

        var page = await entries
            .OrderByDescending(e => e.OccurredAt).ThenByDescending(e => e.Id)
            .Select(e => new
            {
                e.Id,
                e.OccurredAt,
                e.Module,
                e.EntityType,
                e.EntityKey,
                e.Operation,
                e.Actor,
                e.CorrelationId,
                Columns = e.Columns.OrderBy(c => c.Name).Select(c => c.Name).ToList(),
            })
            .ToPagedResultAsync(new PageRequest(request.Page, request.PageSize), cancellationToken);

        return new PagedResult<AuditEntryDto>(
            page.Items.Select(r => new AuditEntryDto(
                r.Id, r.OccurredAt, r.Module, r.EntityType, r.EntityKey, Code(r.Operation), r.Actor, r.CorrelationId, r.Columns)).ToList(),
            page.Page, page.PageSize, page.TotalCount);
    }

    internal static string Code(AuditOperation operation) => operation.ToString().ToUpperInvariant();
}

/// <summary>One entry. A missing id is a not-found, never an empty result.</summary>
public sealed record GetAuditEntryQuery(long Id) : IRequest<AuditEntryDto>;

public sealed class GetAuditEntryQueryHandler(IIdentityDbContext dbContext) : IRequestHandler<GetAuditEntryQuery, AuditEntryDto>
{
    public async Task<AuditEntryDto> Handle(GetAuditEntryQuery request, CancellationToken cancellationToken)
    {
        var row = await dbContext.AuditEntries.AsNoTracking()
            .Where(e => e.Id == request.Id)
            .Select(e => new
            {
                e.Id,
                e.OccurredAt,
                e.Module,
                e.EntityType,
                e.EntityKey,
                e.Operation,
                e.Actor,
                e.CorrelationId,
                Columns = e.Columns.OrderBy(c => c.Name).Select(c => c.Name).ToList(),
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException($"Audit entry {request.Id} was not found.");

        return new AuditEntryDto(
            row.Id, row.OccurredAt, row.Module, row.EntityType, row.EntityKey, ListAuditQueryHandler.Code(row.Operation), row.Actor, row.CorrelationId, row.Columns);
    }
}
