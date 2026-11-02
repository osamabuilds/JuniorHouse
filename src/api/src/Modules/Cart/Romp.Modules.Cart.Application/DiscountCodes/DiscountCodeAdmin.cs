using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Application;
using Romp.BuildingBlocks.Application.Paging;
using Romp.Modules.Cart.Application.Abstractions;
using Romp.Modules.Cart.Domain.DiscountCodes;

namespace Romp.Modules.Cart.Application.DiscountCodes;

/// <summary>A code as staff see it. <see cref="Kind"/> is "percentage" or "fixed"; <see cref="UsedCount"/> counts held and confirmed uses.</summary>
public sealed record DiscountCodeDto(
    long Id, string Code, string Kind, decimal Value, decimal? MinimumSpend, DateTimeOffset? ExpiresAt, int? UsageCap, int? PerCustomerCap,
    bool IncludesSaleItems, bool IsActive, IReadOnlyList<short> CategoryIds, int UsedCount, DateTimeOffset CreatedAt);

/// <summary>The fields staff set on a code (FR-57). Everything is checked by the domain and the message names the mistake (DSC-12).</summary>
public sealed record DiscountCodeInput(
    string Code, string Kind, decimal Value, decimal? MinimumSpend, DateTimeOffset? ExpiresAt, int? UsageCap, int? PerCustomerCap,
    bool IncludesSaleItems, IReadOnlyList<short>? CategoryIds);

public sealed record CreateDiscountCodeCommand(DiscountCodeInput Input) : IRequest<DiscountCodeDto>, ICartCommand;

public sealed record UpdateDiscountCodeCommand(long Id, DiscountCodeInput Input) : IRequest<DiscountCodeDto?>, ICartCommand;

/// <summary>Stops a code working (DSC-9 reads it as unknown); <c>Active</c> false deactivates and true brings it back. A code is never deleted, so history stays.</summary>
public sealed record SetDiscountCodeActiveCommand(long Id, bool Active) : IRequest<DiscountCodeDto?>, ICartCommand;

public sealed record ListDiscountCodesQuery(int Page = 1, int PageSize = PageRequest.DefaultPageSize, bool? Active = null) : IRequest<PagedResult<DiscountCodeDto>>;

public sealed record GetDiscountCodeQuery(long Id) : IRequest<DiscountCodeDto?>;

internal static class DiscountCodeMapping
{
    public static DiscountKind ParseKind(string? kind) =>
        (kind ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "percentage" => DiscountKind.Percentage,
            "fixed" => DiscountKind.Fixed,
            _ => throw new ValidationException(new Dictionary<string, string[]> { ["kind"] = ["Choose percentage or fixed amount."] }),
        };

    public static DiscountCodeDto ToDto(DiscountCode c, int used) =>
        new(
            c.Id, c.Code, c.Kind == DiscountKind.Percentage ? "percentage" : "fixed", c.Value, c.MinimumSpend, c.ExpiresAt, c.UsageCap, c.PerCustomerCap,
            c.IncludesSaleItems, c.IsActive, [.. c.Scope.Select(s => s.CategoryId).Order()], used, c.InsrDte);

    public static ValidationException DuplicateCode() =>
        new(new Dictionary<string, string[]> { ["code"] = ["That code already exists. Codes are not case sensitive."] });
}

public sealed class CreateDiscountCodeCommandHandler(ICartDbContext dbContext, TimeProvider clock) : IRequestHandler<CreateDiscountCodeCommand, DiscountCodeDto>
{
    public async Task<DiscountCodeDto> Handle(CreateDiscountCodeCommand request, CancellationToken cancellationToken)
    {
        var i = request.Input;
        var code = DiscountCode.Create(
            i.Code, DiscountCodeMapping.ParseKind(i.Kind), i.Value, i.MinimumSpend, i.ExpiresAt, i.UsageCap, i.PerCustomerCap, i.IncludesSaleItems, i.CategoryIds ?? [], clock.GetUtcNow());
        if (await dbContext.DiscountCodes.AnyAsync(c => c.Code == code.Code, cancellationToken))
        {
            throw DiscountCodeMapping.DuplicateCode();
        }

        dbContext.DiscountCodes.Add(code);
        await dbContext.SaveChangesAsync(cancellationToken);
        return DiscountCodeMapping.ToDto(code, 0);
    }
}

public sealed class UpdateDiscountCodeCommandHandler(ICartDbContext dbContext, TimeProvider clock) : IRequestHandler<UpdateDiscountCodeCommand, DiscountCodeDto?>
{
    public async Task<DiscountCodeDto?> Handle(UpdateDiscountCodeCommand request, CancellationToken cancellationToken)
    {
        var code = await dbContext.DiscountCodes.Include(c => c.Scope).FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);
        if (code is null)
        {
            return null;
        }

        var i = request.Input;
        var used = await dbContext.Redemptions.CountAsync(r => r.DiscountCodeId == code.Id && r.Status != RedemptionStatus.Released, cancellationToken);
        var newText = DiscountCode.Normalise(i.Code);
        if (newText != code.Code)
        {
            // History and the shopper's receipts name the code, so its text is fixed once anything has used it (DSC-12).
            var everUsed = await dbContext.Redemptions.AnyAsync(r => r.DiscountCodeId == code.Id, cancellationToken);
            if (everUsed)
            {
                throw new ValidationException(new Dictionary<string, string[]> { ["code"] = ["This code has been used, so its text cannot change. Create a new code instead."] });
            }

            if (await dbContext.DiscountCodes.AnyAsync(c => c.Code == newText && c.Id != code.Id, cancellationToken))
            {
                throw DiscountCodeMapping.DuplicateCode();
            }
        }

        code.Update(
            i.Code, DiscountCodeMapping.ParseKind(i.Kind), i.Value, i.MinimumSpend, i.ExpiresAt, i.UsageCap, i.PerCustomerCap, i.IncludesSaleItems, i.CategoryIds ?? [], clock.GetUtcNow());
        return DiscountCodeMapping.ToDto(code, used);
    }
}

public sealed class SetDiscountCodeActiveCommandHandler(ICartDbContext dbContext) : IRequestHandler<SetDiscountCodeActiveCommand, DiscountCodeDto?>
{
    public async Task<DiscountCodeDto?> Handle(SetDiscountCodeActiveCommand request, CancellationToken cancellationToken)
    {
        var code = await dbContext.DiscountCodes.Include(c => c.Scope).FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);
        if (code is null)
        {
            return null;
        }

        if (request.Active)
        {
            code.Reactivate();
        }
        else
        {
            code.Deactivate();
        }

        var used = await dbContext.Redemptions.CountAsync(r => r.DiscountCodeId == code.Id && r.Status != RedemptionStatus.Released, cancellationToken);
        return DiscountCodeMapping.ToDto(code, used);
    }
}

public sealed class ListDiscountCodesQueryHandler(ICartDbContext dbContext) : IRequestHandler<ListDiscountCodesQuery, PagedResult<DiscountCodeDto>>
{
    public async Task<PagedResult<DiscountCodeDto>> Handle(ListDiscountCodesQuery request, CancellationToken cancellationToken)
    {
        var codes = dbContext.DiscountCodes.AsNoTracking().Include(c => c.Scope).AsQueryable();
        if (request.Active is { } active)
        {
            codes = codes.Where(c => c.IsActive == active);
        }

        var page = await codes.OrderByDescending(c => c.Id).ToPagedResultAsync(new PageRequest(request.Page, request.PageSize), cancellationToken);

        // Use counts for the whole page in one grouped query (no N+1).
        var ids = page.Items.Select(c => c.Id).ToList();
        var used = await dbContext.Redemptions.AsNoTracking()
            .Where(r => ids.Contains(r.DiscountCodeId) && r.Status != RedemptionStatus.Released)
            .GroupBy(r => r.DiscountCodeId)
            .Select(g => new { Id = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Id, x => x.Count, cancellationToken);
        return new PagedResult<DiscountCodeDto>(
            [.. page.Items.Select(c => DiscountCodeMapping.ToDto(c, used.GetValueOrDefault(c.Id)))], page.Page, page.PageSize, page.TotalCount);
    }
}

public sealed class GetDiscountCodeQueryHandler(ICartDbContext dbContext) : IRequestHandler<GetDiscountCodeQuery, DiscountCodeDto?>
{
    public async Task<DiscountCodeDto?> Handle(GetDiscountCodeQuery request, CancellationToken cancellationToken)
    {
        var code = await dbContext.DiscountCodes.AsNoTracking().Include(c => c.Scope).FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);
        if (code is null)
        {
            return null;
        }

        var used = await dbContext.Redemptions.CountAsync(r => r.DiscountCodeId == code.Id && r.Status != RedemptionStatus.Released, cancellationToken);
        return DiscountCodeMapping.ToDto(code, used);
    }
}
