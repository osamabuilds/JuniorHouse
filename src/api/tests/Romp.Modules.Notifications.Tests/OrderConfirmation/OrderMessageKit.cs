using Romp.Modules.Notifications.Application.Channels;
using Romp.Modules.Notifications.Application.Delivery;
using Romp.Modules.Notifications.Infrastructure.Channels;
using Romp.Modules.Notifications.Infrastructure.Delivery;
using Romp.Modules.Notifications.Infrastructure.Persistence;
using Romp.Modules.Notifications.Tests.Support;
using Romp.Modules.Orders.Contracts;

namespace Romp.Modules.Notifications.Tests.OrderConfirmation;

internal static class OrderMessageKit
{
    public static OrderNotificationView Order(
        string payment = "COD", decimal total = 1500m, string? reason = null, string? confirmUrl = null, string name = "Ayesha", string? email = "a@example.com", string mobile = "+923001234567") =>
        new(1, "RMP-0001", payment, name, email, mobile, true, total, [new OrderNotificationLine("Red Dress", "Red", "4-5Y", 2, total)], reason, confirmUrl);

    /// <summary>The real delivery processor with the real template renderer, so the words come from the seeded templates.</summary>
    public static DeliveryProcessor Processor(NtfyDbContext db, TestClock clock, IChannelSender sender, IOrderNotificationQueries orders) =>
        new(db, new SkipLockedDeliveryClaimer(db), orders, new TemplateMessageRenderer(db), new SuppressionList(db, new WhatsAppSettings()), new SqlDailySendCounter(db, clock), [sender], new DeliverySettings(), clock);
}

internal sealed class FixedOrder(Func<OrderNotificationView> order) : IOrderNotificationQueries
{
    public Task<OrderNotificationView?> FindAsync(long orderId, CancellationToken cancellationToken) => Task.FromResult<OrderNotificationView?>(order());
}
