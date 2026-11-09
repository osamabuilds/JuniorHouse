using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Application.Paging;
using Romp.BuildingBlocks.Domain;
using Romp.Modules.Shipping.Application.Abstractions;
using Romp.Modules.Shipping.Application.CourierPort;
using Romp.Modules.Shipping.Domain;
using Romp.Modules.Shipping.Domain.Manifests;

namespace Romp.Modules.Shipping.Application.Manifests;

public sealed record ManifestSummaryDto(long Id, string CourierName, DateOnly BusinessDate, string Status, int ParcelCount, int? CountedParcels, string? CountedBy, DateTimeOffset? CountedAt);

public sealed record ManifestLineDto(long ShipmentId, string OrderNumber, string Waybill, decimal CashToCollect, int WeightGrams, bool HandedOver);

public sealed record ManifestDetailDto(ManifestSummaryDto Summary, string? Note, decimal TotalCash, int TotalWeightGrams, IReadOnlyList<ManifestLineDto> Lines);

/// <summary>The business day in Pakistan (UTC+5, no daylight saving): the day a manifest belongs to.</summary>
public static class BusinessDay
{
    public static DateOnly Of(DateTimeOffset instant) => DateOnly.FromDateTime(instant.ToOffset(TimeSpan.FromHours(5)).DateTime);
}

/// <summary>
/// Makes the manifests of a day (MNF-1, MNF-2, MNF-4): every booked parcel the courier has not collected and that is on no manifest goes on the open
/// manifest of its courier for the day, created if needed. Generating again adds only the new parcels. A day whose manifest was already counted takes
/// no more: those parcels wait for the next day's (MNF-4, MNF-7).
/// </summary>
public sealed record GenerateManifestsCommand(DateOnly? Date = null, short? CourierId = null) : IRequest<IReadOnlyList<ManifestSummaryDto>>, IShippingCommand;

public sealed class GenerateManifestsCommandHandler(IShippingDbContext dbContext, ICourierRegistry couriers, TimeProvider clock)
    : IRequestHandler<GenerateManifestsCommand, IReadOnlyList<ManifestSummaryDto>>
{
    public async Task<IReadOnlyList<ManifestSummaryDto>> Handle(GenerateManifestsCommand request, CancellationToken cancellationToken)
    {
        var date = request.Date ?? BusinessDay.Of(clock.GetUtcNow());
        var made = new List<Manifest>();
        foreach (var courier in couriers.InOrder.Where(c => request.CourierId is null || c.CourierId == request.CourierId))
        {
            var courierId = courier.CourierId;
            var parcels = await dbContext.Shipments.Where(s => s.CourierId == courierId && s.StatusId == ShipmentStatuses.Booked && s.ManifestId == null)
                .OrderBy(s => s.Id).ToListAsync(cancellationToken);
            if (parcels.Count == 0)
            {
                continue;
            }

            var manifest = await dbContext.Manifests.Include(m => m.Lines).FirstOrDefaultAsync(m => m.CourierId == courierId && m.BusinessDate == date, cancellationToken);
            if (manifest is null)
            {
                manifest = Manifest.Open(courierId, date);
                dbContext.Manifests.Add(manifest);
            }
            else if (manifest.StatusId != ManifestStatuses.Open)
            {
                continue;
            }

            foreach (var parcel in parcels)
            {
                manifest.Add(parcel.Id);
            }

            // The parcels need the manifest's id, so it is saved first; the behaviour saves the parcels after.
            await dbContext.SaveChangesAsync(cancellationToken);
            foreach (var parcel in parcels)
            {
                parcel.PutOnManifest(manifest.Id);
            }

            made.Add(manifest);
        }

        return [.. made.Select(m => ManifestMapper.Summary(m, couriers.Find(m.CourierId)?.CourierId is { } id ? Couriers.NameOf(id) : "Courier"))];
    }
}

/// <summary>Staff enter the number of parcels the courier took (MNF-5): equal is Matched and final, different is a Mismatch to resolve.</summary>
public sealed record CountManifestCommand(long ManifestId, int Parcels, string Staff) : IRequest<ManifestSummaryDto?>, IShippingCommand;

public sealed class CountManifestCommandHandler(IShippingDbContext dbContext, TimeProvider clock) : IRequestHandler<CountManifestCommand, ManifestSummaryDto?>
{
    public async Task<ManifestSummaryDto?> Handle(CountManifestCommand request, CancellationToken cancellationToken)
    {
        var manifest = await dbContext.Manifests.Include(m => m.Lines).FirstOrDefaultAsync(m => m.Id == request.ManifestId, cancellationToken);
        if (manifest is null)
        {
            return null;
        }

        manifest.Count(request.Parcels, request.Staff, clock.GetUtcNow());
        return ManifestMapper.Summary(manifest, Couriers.NameOf(manifest.CourierId));
    }
}

/// <summary>After a mismatch, staff say whether a parcel was handed over (MNF-6).</summary>
public sealed record MarkHandedOverCommand(long ManifestId, long ShipmentId, bool HandedOver, string Staff) : IRequest<bool>, IShippingCommand;

public sealed class MarkHandedOverCommandHandler(IShippingDbContext dbContext) : IRequestHandler<MarkHandedOverCommand, bool>
{
    public async Task<bool> Handle(MarkHandedOverCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Staff))
        {
            throw new DomainException("A signed-in staff member is required.");
        }

        var manifest = await dbContext.Manifests.Include(m => m.Lines).FirstOrDefaultAsync(m => m.Id == request.ManifestId, cancellationToken);
        if (manifest is null)
        {
            return false;
        }

        manifest.MarkHandedOver(request.ShipmentId, request.HandedOver);
        return true;
    }
}

/// <summary>Closes a mismatch with a note (MNF-7). Parcels not handed over come off the manifest and wait for the next one (MNF-6).</summary>
public sealed record ResolveManifestCommand(long ManifestId, string Note, string Staff) : IRequest<ManifestSummaryDto?>, IShippingCommand;

public sealed class ResolveManifestCommandHandler(IShippingDbContext dbContext) : IRequestHandler<ResolveManifestCommand, ManifestSummaryDto?>
{
    public async Task<ManifestSummaryDto?> Handle(ResolveManifestCommand request, CancellationToken cancellationToken)
    {
        var manifest = await dbContext.Manifests.Include(m => m.Lines).FirstOrDefaultAsync(m => m.Id == request.ManifestId, cancellationToken);
        if (manifest is null)
        {
            return null;
        }

        var notHandedOver = manifest.Resolve(request.Note, request.Staff);
        if (notHandedOver.Count > 0)
        {
            foreach (var shipment in await dbContext.Shipments.Where(s => notHandedOver.Contains(s.Id)).ToListAsync(cancellationToken))
            {
                shipment.TakeOffManifest();
            }
        }

        return ManifestMapper.Summary(manifest, Couriers.NameOf(manifest.CourierId));
    }
}

/// <summary>The manifests, newest first, paged (MNF-10). One count and one page query.</summary>
public sealed record ListManifestsQuery(int Page = 1, int PageSize = PageRequest.DefaultPageSize) : IRequest<PagedResult<ManifestSummaryDto>>;

public sealed class ListManifestsQueryHandler(IShippingDbContext dbContext) : IRequestHandler<ListManifestsQuery, PagedResult<ManifestSummaryDto>>
{
    public async Task<PagedResult<ManifestSummaryDto>> Handle(ListManifestsQuery request, CancellationToken cancellationToken)
    {
        var page = await dbContext.Manifests.AsNoTracking().OrderByDescending(m => m.BusinessDate).ThenByDescending(m => m.Id)
            .Select(m => new { m.Id, m.CourierId, m.BusinessDate, m.StatusId, Count = m.Lines.Count, m.CountedParcels, m.CountedBy, m.CountedAt })
            .ToPagedResultAsync(new PageRequest(request.Page, request.PageSize), cancellationToken);
        return new PagedResult<ManifestSummaryDto>(
            [.. page.Items.Select(m => new ManifestSummaryDto(m.Id, Couriers.NameOf(m.CourierId), m.BusinessDate, ManifestStatuses.CodeOf(m.StatusId), m.Count, m.CountedParcels, m.CountedBy, m.CountedAt))],
            page.Page, page.PageSize, page.TotalCount);
    }
}

/// <summary>One manifest with its lines, for the printed page (MNF-1, MNF-8): two queries however many parcels.</summary>
public sealed record GetManifestQuery(long ManifestId) : IRequest<ManifestDetailDto?>;

public sealed class GetManifestQueryHandler(IShippingDbContext dbContext) : IRequestHandler<GetManifestQuery, ManifestDetailDto?>
{
    public async Task<ManifestDetailDto?> Handle(GetManifestQuery request, CancellationToken cancellationToken)
    {
        var manifest = await dbContext.Manifests.AsNoTracking().Include(m => m.Lines).FirstOrDefaultAsync(m => m.Id == request.ManifestId, cancellationToken);
        if (manifest is null)
        {
            return null;
        }

        var ids = manifest.Lines.Select(l => l.ShipmentId).ToList();
        var parcels = await dbContext.Shipments.AsNoTracking().Where(s => ids.Contains(s.Id))
            .Select(s => new { s.Id, s.OrderNumber, s.Waybill, s.CashToCollect, s.WeightGrams }).ToDictionaryAsync(s => s.Id, cancellationToken);
        var lines = manifest.Lines.OrderBy(l => l.ShipmentId)
            .Where(l => parcels.ContainsKey(l.ShipmentId))
            .Select(l => new ManifestLineDto(l.ShipmentId, parcels[l.ShipmentId].OrderNumber, parcels[l.ShipmentId].Waybill ?? string.Empty, parcels[l.ShipmentId].CashToCollect, parcels[l.ShipmentId].WeightGrams, l.HandedOver))
            .ToList();
        return new ManifestDetailDto(ManifestMapper.Summary(manifest, Couriers.NameOf(manifest.CourierId)), manifest.Note, lines.Sum(l => l.CashToCollect), lines.Sum(l => l.WeightGrams), lines);
    }
}

internal static class ManifestMapper
{
    public static ManifestSummaryDto Summary(Manifest m, string courierName) =>
        new(m.Id, courierName, m.BusinessDate, ManifestStatuses.CodeOf(m.StatusId), m.Lines.Count, m.CountedParcels, m.CountedBy, m.CountedAt);
}
