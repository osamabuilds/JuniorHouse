using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Application.Paging;
using Romp.Modules.Identity.Application.Abstractions;

namespace Romp.Modules.Identity.Application.StaffAccounts;

/// <summary>A role an account holds, as shown next to the account.</summary>
public sealed record StaffRoleDto(short Id, string Code, string Name);

/// <summary>
/// An account as the back office sees it (AC-3). It has no hash and no password of any kind; only whether a temporary password is
/// outstanding and when it lapses.
/// </summary>
public sealed record StaffDto(
    long Id,
    string UserName,
    string DisplayName,
    bool IsActive,
    bool MustChangePassword,
    bool HasTemporaryPassword,
    DateTimeOffset? TemporaryPasswordExpiresAt,
    IReadOnlyList<StaffRoleDto> Roles,
    DateTimeOffset CreatedAt);

/// <summary>
/// The paged account list: an optional search over the user name or display name, and an optional active filter. Ordered by user name
/// then id (both unique together), so pages never overlap. One COUNT and one page query: the roles are part of the same SELECT.
/// </summary>
public sealed record ListStaffQuery(string? Search = null, bool? IsActive = null, int Page = 1, int PageSize = PageRequest.DefaultPageSize)
    : IRequest<PagedResult<StaffDto>>;

public sealed class ListStaffQueryValidator : AbstractValidator<ListStaffQuery>
{
    public const int MaxPageSize = 100;
    public const int MaxSearchLength = 100;

    public ListStaffQueryValidator()
    {
        RuleFor(q => q.Page).GreaterThanOrEqualTo(1);
        RuleFor(q => q.PageSize).InclusiveBetween(1, MaxPageSize);
        RuleFor(q => q.Search).MaximumLength(MaxSearchLength);
    }
}

public sealed class ListStaffQueryHandler(IIdentityDbContext dbContext) : IRequestHandler<ListStaffQuery, PagedResult<StaffDto>>
{
    public async Task<PagedResult<StaffDto>> Handle(ListStaffQuery request, CancellationToken cancellationToken)
    {
        var staff = dbContext.Staff.AsNoTracking().AsQueryable();
        if (request.IsActive is { } active)
        {
            staff = staff.Where(u => u.IsActive == active);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
#pragma warning disable CA1304, CA1311, CA1862 // translated to SQL lower(), which has no culture; the StringComparison overload is not translatable
            var text = request.Search.Trim().ToLower();
            staff = staff.Where(u => u.UserName.Contains(text) || u.DisplayName.ToLower().Contains(text));
#pragma warning restore CA1304, CA1311, CA1862
        }

        var page = await staff
            .OrderBy(u => u.UserName).ThenBy(u => u.Id)
            .Select(u => new
            {
                u.Id,
                u.UserName,
                u.DisplayName,
                u.IsActive,
                u.MustChangePassword,
                u.TemporaryPasswordExpiresAt,
                Roles = dbContext.Roles
                    .Where(r => u.Roles.Any(m => m.RoleId == r.Id))
                    .OrderBy(r => r.SortSeq)
                    .Select(r => new StaffRoleDto(r.Id, r.Code, r.Name))
                    .ToList(),
                u.InsrDte,
            })
            .ToPagedResultAsync(new PageRequest(request.Page, request.PageSize), cancellationToken);

        return new PagedResult<StaffDto>(
            page.Items.Select(r => new StaffDto(
                r.Id, r.UserName, r.DisplayName, r.IsActive, r.MustChangePassword, r.TemporaryPasswordExpiresAt is not null,
                r.TemporaryPasswordExpiresAt, r.Roles, r.InsrDte)).ToList(),
            page.Page, page.PageSize, page.TotalCount);
    }
}

/// <summary>One account with its roles. A missing id is a not-found, never an empty result.</summary>
public sealed record GetStaffQuery(long UserId) : IRequest<StaffDto>;

public sealed class GetStaffQueryHandler(IIdentityDbContext dbContext) : IRequestHandler<GetStaffQuery, StaffDto>
{
    public async Task<StaffDto> Handle(GetStaffQuery request, CancellationToken cancellationToken)
    {
        var row = await dbContext.Staff.AsNoTracking()
            .Where(u => u.Id == request.UserId)
            .Select(u => new
            {
                u.Id,
                u.UserName,
                u.DisplayName,
                u.IsActive,
                u.MustChangePassword,
                u.TemporaryPasswordExpiresAt,
                Roles = dbContext.Roles
                    .Where(r => u.Roles.Any(m => m.RoleId == r.Id))
                    .OrderBy(r => r.SortSeq)
                    .Select(r => new StaffRoleDto(r.Id, r.Code, r.Name))
                    .ToList(),
                u.InsrDte,
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException($"Staff account {request.UserId} was not found.");

        return new StaffDto(
            row.Id, row.UserName, row.DisplayName, row.IsActive, row.MustChangePassword, row.TemporaryPasswordExpiresAt is not null,
            row.TemporaryPasswordExpiresAt, row.Roles, row.InsrDte);
    }
}
