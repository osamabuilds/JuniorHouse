using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Application.Paging;
using Romp.Modules.Warehouse.Application.Abstractions;
using Romp.Modules.Warehouse.Domain.Reservations;

namespace Romp.Modules.Warehouse.Application.Reservations;

/// <summary>
/// Gives back the units of holds whose order never confirmed them within <c>Warehouse:ReservationHoldMinutes</c> (RSV-6, ADR 0025). A
/// confirmed reservation is never swept. A small batch per round keeps the transaction short. Returns how many were released.
/// </summary>
public sealed record SweepHeldReservationsCommand(int BatchSize = SweepHeldReservationsCommand.DefaultBatchSize) : IRequest<int>, IWarehouseCommand
{
    public const int DefaultBatchSize = 100;
}

public sealed class SweepHeldReservationsCommandHandler(IWarehouseDbContext dbContext, IReservationLedger ledger, TimeProvider clock)
    : IRequestHandler<SweepHeldReservationsCommand, int>
{
    public async Task<int> Handle(SweepHeldReservationsCommand request, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var expired = await dbContext.Reservations.Include(r => r.Lines)
            .Where(r => r.StatusId == ReservationStatuses.Held && r.HeldUntil != null && r.HeldUntil < now)
            .OrderBy(r => r.HeldUntil)
            .Take(Math.Clamp(request.BatchSize, 1, 500))
            .ToListAsync(cancellationToken);

        var released = 0;
        foreach (var reservation in expired)
        {
            if (reservation.Release() != ReservationTransition.Changed)
            {
                continue;
            }

            await ledger.ReleaseAsync([.. reservation.Lines.OrderBy(l => l.VariantRefId).Select(l => new ReservedLine(l.VariantRefId, l.Quantity))], ReservationReasons.Expired, cancellationToken);
            released++;
        }

        return released;
    }
}

/// <summary>
/// The hook the pick confirmation will call (RSV-9, built in S14): the reservation becomes <c>Converted</c> and its reserved units are
/// dropped from the reserved balance, in the same transaction as the ledger decrement of the pick, so available does not move twice.
/// Returns false when there is nothing to convert (unknown key, not confirmed, or already converted).
/// </summary>
public sealed record ConvertReservationCommand(string PlacementKey) : IRequest<bool>, IWarehouseCommand;

public sealed class ConvertReservationCommandHandler(IWarehouseDbContext dbContext, IReservationLedger ledger) : IRequestHandler<ConvertReservationCommand, bool>
{
    public const string Reason = "RESERVATION_CONVERTED";

    public async Task<bool> Handle(ConvertReservationCommand request, CancellationToken cancellationToken)
    {
        var reservation = await dbContext.Reservations.Include(r => r.Lines).FirstOrDefaultAsync(r => r.PlacementKey == request.PlacementKey, cancellationToken);
        if (reservation is null || reservation.Convert() != ReservationTransition.Changed)
        {
            return false;
        }

        await ledger.ReleaseAsync([.. reservation.Lines.OrderBy(l => l.VariantRefId).Select(l => new ReservedLine(l.VariantRefId, l.Quantity))], Reason, cancellationToken);
        return true;
    }
}

/// <summary>One reservation touching a variant, for staff. Order numbers only, never a customer (SEC-25).</summary>
public sealed record VariantReservationDto(string PlacementKey, string StatusCode, int Quantity, string? OrderNumber, DateTimeOffset? HeldUntil, DateTimeOffset CreatedAt);

/// <summary>The reservations holding a variant, newest first and paged, with the status as its stable code.</summary>
public sealed record ListVariantReservationsQuery(long VariantRefId, int Page = 1, int PageSize = PageRequest.DefaultPageSize)
    : IRequest<PagedResult<VariantReservationDto>>;

public sealed class ListVariantReservationsQueryHandler(IWarehouseDbContext dbContext)
    : IRequestHandler<ListVariantReservationsQuery, PagedResult<VariantReservationDto>>
{
    public async Task<PagedResult<VariantReservationDto>> Handle(ListVariantReservationsQuery request, CancellationToken cancellationToken)
    {
        var page = await dbContext.Reservations.AsNoTracking()
            .Where(r => r.Lines.Any(l => l.VariantRefId == request.VariantRefId))
            .OrderByDescending(r => r.Id)
            .Select(r => new
            {
                r.PlacementKey,
                r.StatusId,
                Quantity = r.Lines.Where(l => l.VariantRefId == request.VariantRefId).Sum(l => l.Quantity),
                r.OrderNumber,
                r.HeldUntil,
                r.InsrDte,
            })
            .ToPagedResultAsync(new PageRequest(request.Page, request.PageSize), cancellationToken);

        var items = page.Items.Select(i => new VariantReservationDto(i.PlacementKey, StatusCode(i.StatusId), i.Quantity, i.OrderNumber, i.HeldUntil, i.InsrDte)).ToList();
        return new PagedResult<VariantReservationDto>(items, page.Page, page.PageSize, page.TotalCount);
    }

    private static string StatusCode(short id) => id switch
    {
        ReservationStatuses.Held => "HELD",
        ReservationStatuses.Confirmed => "CONFIRMED",
        ReservationStatuses.Converted => "CONVERTED",
        _ => "RELEASED",
    };
}
