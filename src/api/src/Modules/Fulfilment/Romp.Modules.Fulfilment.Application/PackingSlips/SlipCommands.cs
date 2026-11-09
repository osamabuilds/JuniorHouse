using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.Modules.Fulfilment.Application.Abstractions;
using Romp.Modules.Fulfilment.Domain.Allocations;
using Romp.Modules.Orders.Contracts;
using Romp.Modules.Storefront.Contracts;

namespace Romp.Modules.Fulfilment.Application.PackingSlips;

public sealed record SlipLineDto(string? Sku, string Name, string Colour, string Size, int Quantity, decimal UnitPrice, decimal LineTotal);

/// <summary>
/// The slip that goes in the parcel (SLP-1 to SLP-8): items, prices, the cash to collect or "Paid", how to return, and who to ask. It shows the
/// recipient's name and city only, never the phone number or email (SLP-7). The exchange window and contact come from the shop settings (SLP-4).
/// </summary>
public sealed record PackingSlipDto(
    string OrderNumber, DateTimeOffset PlacedAt, string RecipientName, string City, IReadOnlyList<SlipLineDto> Lines,
    decimal Subtotal, decimal Discount, decimal Shipping, decimal Total, bool IsCashOnDelivery, decimal CashToCollect, bool IsPaid,
    string BrandName, int ExchangeWindowDays, string? SupportWhatsapp, string? SupportEmail, string? SupportHours, bool IsReprint, int PrintCount, bool PrintPending);

/// <summary>Builds a slip from Orders and the shop settings at the moment of need, so it is never stale and nothing personal is stored here.</summary>
public sealed class PackingSlipBuilder(IOrderFulfilmentQueries orders, IShopSettingsQueries settings)
{
    public async Task<PackingSlipDto?> BuildAsync(FulfilmentOrder order, CancellationToken cancellationToken)
    {
        var view = await orders.FindAsync(order.OrderId, cancellationToken);
        if (view is null)
        {
            return null;
        }

        var shop = await settings.GetAsync(cancellationToken);
        return new PackingSlipDto(
            view.OrderNumber, view.PlacedAt, view.RecipientName, view.City,
            [.. view.Lines.Select(l => new SlipLineDto(l.Sku, l.Name, l.Colour, l.Size, l.Quantity, l.UnitPrice, l.LineTotal))],
            view.Subtotal, view.Discount, view.Shipping, view.Total, view.IsCashOnDelivery, view.CashToCollect, !view.IsCashOnDelivery,
            shop?.BrandName ?? "Romp", shop?.ExchangeWindowDays ?? 0, shop?.Whatsapp, shop?.Email, shop?.Hours,
            order.SlipPrintCount > 0, order.SlipPrintCount, order.SlipPrintPending);
    }
}

/// <summary>
/// Sends a slip to the printer (SLP-6). A printer that is down returns false and never throws, so the pack is never blocked: the slip stays pending and
/// prints on retry. The real device sits behind this port (docs/roadmap/third-party-integrations.md).
/// </summary>
public interface ISlipPrinter
{
    Task<bool> PrintAsync(PackingSlipDto slip, CancellationToken cancellationToken);
}

public sealed record GetPackingSlipQuery(long OrderId) : IRequest<PackingSlipDto?>;

public sealed class GetPackingSlipQueryHandler(IFulfilmentDbContext dbContext, PackingSlipBuilder builder) : IRequestHandler<GetPackingSlipQuery, PackingSlipDto?>
{
    public async Task<PackingSlipDto?> Handle(GetPackingSlipQuery request, CancellationToken cancellationToken)
    {
        var order = await dbContext.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken);
        return order is null || !order.CanPrintSlip ? null : await builder.BuildAsync(order, cancellationToken);
    }
}

/// <summary>Print (or reprint) the slip. The order is not changed by printing: only the print count and pending mark (SLP-5).</summary>
public sealed record PrintSlipResult(PackingSlipDto Slip, bool Printed);

public sealed record PrintPackingSlipCommand(long OrderId) : IRequest<PrintSlipResult?>, IFulfilmentCommand;

public sealed class PrintPackingSlipCommandHandler(IFulfilmentDbContext dbContext, PackingSlipBuilder builder, ISlipPrinter printer, TimeProvider clock)
    : IRequestHandler<PrintPackingSlipCommand, PrintSlipResult?>
{
    public async Task<PrintSlipResult?> Handle(PrintPackingSlipCommand request, CancellationToken cancellationToken)
    {
        var order = await dbContext.Orders.FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken);
        if (order is null || !order.CanPrintSlip)
        {
            return null;
        }

        var slip = await builder.BuildAsync(order, cancellationToken);
        if (slip is null)
        {
            return null;
        }

        var printed = await SlipPrinting.TryAsync(printer, slip, cancellationToken);
        if (printed)
        {
            order.RecordSlipPrinted(clock.GetUtcNow());
        }
        else
        {
            order.MarkSlipPrintPending();
        }

        return new PrintSlipResult(slip with { PrintPending = !printed, PrintCount = order.SlipPrintCount }, printed);
    }
}

internal static class SlipPrinting
{
    /// <summary>A printer fault of any kind is a failed print, not a failed pack (SLP-6).</summary>
    public static async Task<bool> TryAsync(ISlipPrinter printer, PackingSlipDto slip, CancellationToken cancellationToken)
    {
        try
        {
            return await printer.PrintAsync(slip, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return false;
        }
    }
}

/// <summary>Tries again the slips whose printer was down (SLP-6). Called by a background worker; returns how many printed.</summary>
public sealed record RetryPendingSlipsCommand(int Batch = 25) : IRequest<int>, IFulfilmentCommand;

public sealed class RetryPendingSlipsCommandHandler(IFulfilmentDbContext dbContext, PackingSlipBuilder builder, ISlipPrinter printer, TimeProvider clock)
    : IRequestHandler<RetryPendingSlipsCommand, int>
{
    public async Task<int> Handle(RetryPendingSlipsCommand request, CancellationToken cancellationToken)
    {
        var pending = await dbContext.Orders.Where(o => o.SlipPrintPending).OrderBy(o => o.Id).Take(request.Batch).ToListAsync(cancellationToken);
        var printed = 0;
        foreach (var order in pending)
        {
            var slip = await builder.BuildAsync(order, cancellationToken);
            if (slip is not null && await SlipPrinting.TryAsync(printer, slip, cancellationToken))
            {
                order.RecordSlipPrinted(clock.GetUtcNow());
                printed++;
            }
        }

        return printed;
    }
}
