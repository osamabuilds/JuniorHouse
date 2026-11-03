using System.Globalization;
using System.Net;
using System.Text;
using Romp.Modules.Notifications.Domain.Delivery;
using Romp.Modules.Orders.Contracts;

namespace Romp.Modules.Notifications.Application.Delivery;

/// <summary>
/// Placeholder wording until the templates feature (H2) arrives and the business supplies copy (Q-OCF-1). It already follows the rules the
/// real renderer keeps: values come from the order, are HTML-encoded in the HTML twin, and WhatsApp text is stripped of control characters.
/// </summary>
public sealed class PlainMessageRenderer : IMessageRenderer
{
    public Task<RenderedMessage> RenderAsync(NotificationKind kind, NotificationChannel channel, OrderNotificationView order, CancellationToken cancellationToken) =>
        Task.FromResult(RenderPlain(kind, channel, order));

    public static RenderedMessage RenderPlain(NotificationKind kind, NotificationChannel channel, OrderNotificationView order)
    {
        ArgumentNullException.ThrowIfNull(order);

        var (subject, intro) = kind switch
        {
            NotificationKind.OrderConfirmation => ($"Your Romp order {order.OrderNumber}", "Thank you for your order."),
            NotificationKind.OrderCancelled => ($"Your Romp order {order.OrderNumber} was cancelled", "Your order has been cancelled."),
            NotificationKind.CodConfirmationRequest => ($"Please confirm your Romp order {order.OrderNumber}", "Please confirm your cash on delivery order."),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };

        var total = string.Create(CultureInfo.InvariantCulture, $"PKR {order.Total:N2}");
        var text = new StringBuilder()
            .Append(Clean(intro)).Append("\n\nOrder ").Append(Clean(order.OrderNumber)).Append('\n');
        foreach (var line in order.Lines)
        {
            text.Append("- ").Append(line.Quantity.ToString(CultureInfo.InvariantCulture)).Append(" x ").Append(Clean(line.Name))
                .Append(" (").Append(Clean(line.Colour)).Append(", ").Append(Clean(line.Size)).Append(")\n");
        }

        text.Append("Total: ").Append(total);

        if (channel == NotificationChannel.WhatsApp)
        {
            return new RenderedMessage(subject, text.ToString(), null);
        }

        var html = new StringBuilder()
            .Append("<p>").Append(WebUtility.HtmlEncode(intro)).Append("</p><p>Order ").Append(WebUtility.HtmlEncode(order.OrderNumber)).Append("</p><ul>");
        foreach (var line in order.Lines)
        {
            html.Append("<li>").Append(line.Quantity.ToString(CultureInfo.InvariantCulture)).Append(" x ").Append(WebUtility.HtmlEncode(line.Name))
                .Append(" (").Append(WebUtility.HtmlEncode(line.Colour)).Append(", ").Append(WebUtility.HtmlEncode(line.Size)).Append(")</li>");
        }

        html.Append("</ul><p>Total: ").Append(WebUtility.HtmlEncode(total)).Append("</p>");
        return new RenderedMessage(subject, text.ToString(), html.ToString());
    }

    private static string Clean(string value) => new(value.Where(c => !char.IsControl(c)).ToArray());
}
