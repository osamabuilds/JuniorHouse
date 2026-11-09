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
            NotificationKind.OrderShipped => ($"Your Romp order {order.OrderNumber} is on its way", "The courier has collected your parcel."),
            NotificationKind.DeliveryAttemptFailed => ($"We could not deliver your Romp order {order.OrderNumber}", "The courier tried to deliver your parcel and could not. Please make sure your phone is on and someone can receive it. The courier will try again; if it fails too often the parcel comes back to us."),
            NotificationKind.OrderDelayed => ($"Part of your Romp order {order.OrderNumber} is delayed", "One item in your order is waiting for stock, so it will ship a little later. We will tell you as soon as it is on its way."),
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
        var trackLink = kind is NotificationKind.OrderShipped or NotificationKind.DeliveryAttemptFailed && !string.IsNullOrWhiteSpace(order.TrackUrl) ? Clean(order.TrackUrl) : null;
        if (trackLink is not null)
        {
            text.Append("\nTrack your parcel: ").Append(trackLink);
        }

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
        if (trackLink is not null)
        {
            html.Append("<p><a href=\"").Append(WebUtility.HtmlEncode(trackLink)).Append("\">Track your parcel</a></p>");
        }

        return new RenderedMessage(subject, text.ToString(), html.ToString());
    }

    private static string Clean(string value) => new(value.Where(c => !char.IsControl(c)).ToArray());
}
