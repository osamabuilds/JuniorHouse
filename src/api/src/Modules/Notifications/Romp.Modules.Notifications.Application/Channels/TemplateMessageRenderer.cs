using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Romp.Modules.Notifications.Application.Abstractions;
using Romp.Modules.Notifications.Application.Delivery;
using Romp.Modules.Notifications.Domain.Delivery;
using Romp.Modules.Orders.Contracts;

namespace Romp.Modules.Notifications.Application.Channels;

/// <summary>
/// Fills a template with values from the order, in one pass (a value that looks like <c>{{Total}}</c> is never expanded again). Every value is
/// HTML-encoded in the HTML twin, and stripped of control characters everywhere, so a hostile product or customer name cannot inject markup, a
/// header or a new line into a subject (CHN-9, OCF-7). WhatsApp values are also length-capped.
/// Placeholders: <c>{{OrderNumber}}</c>, <c>{{CustomerName}}</c>, <c>{{Total}}</c>, <c>{{Lines}}</c>.
/// </summary>
public static partial class TemplateEngine
{
    private const int WhatsAppValueCap = 200;
    private const int WhatsAppLinesCap = 1000;

    public static RenderedMessage Render(MessageTemplate template, OrderNotificationView order)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(order);

        var whatsApp = template.Channel == NotificationChannel.WhatsApp;
        var total = string.Create(CultureInfo.InvariantCulture, $"PKR {order.Total:N2}");

        string Plain(string value) => Cap(Clean(value), whatsApp ? WhatsAppValueCap : int.MaxValue);

        var textValues = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["OrderNumber"] = Plain(order.OrderNumber),
            ["CustomerName"] = Plain(order.ContactName),
            ["Total"] = total,
            ["Lines"] = Cap(TextLines(order), whatsApp ? WhatsAppLinesCap : int.MaxValue),
        };

        var subject = Fill(template.Subject, textValues);
        var text = Fill(template.PlainText, textValues);

        if (whatsApp)
        {
            // The provider's template has fixed numbered placeholders: 1 customer name, 2 order number, 3 total (CHN-13). The COD request carries
            // the two reply buttons whose ids come back in the reply (CHN-12).
            IReadOnlyList<WhatsAppButton>? buttons = template.Kind == NotificationKind.CodConfirmationRequest
                ? [new WhatsAppButton(CodButtons.Confirm, "Confirm order"), new WhatsAppButton(CodButtons.Cancel, "Cancel order")]
                : null;
            return new RenderedMessage(subject, text, null, template.ProviderTemplateName, [textValues["CustomerName"], textValues["OrderNumber"], total], buttons);
        }

        string? html = null;
        if (!whatsApp && !string.IsNullOrWhiteSpace(template.Html))
        {
            var htmlValues = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["OrderNumber"] = WebUtility.HtmlEncode(Clean(order.OrderNumber)),
                ["CustomerName"] = WebUtility.HtmlEncode(Clean(order.ContactName)),
                ["Total"] = WebUtility.HtmlEncode(total),
                ["Lines"] = HtmlLines(order),
            };
            html = Fill(template.Html, htmlValues);
        }

        return new RenderedMessage(subject, text, html);
    }

    private static string Fill(string template, Dictionary<string, string> values) =>
        Placeholder().Replace(template, m => values.TryGetValue(m.Groups[1].Value, out var v) ? v : string.Empty);

    private static string TextLines(OrderNotificationView order)
    {
        var sb = new StringBuilder();
        foreach (var line in order.Lines)
        {
            sb.Append("- ").Append(line.Quantity.ToString(CultureInfo.InvariantCulture)).Append(" x ").Append(Clean(line.Name))
                .Append(" (").Append(Clean(line.Colour)).Append(", ").Append(Clean(line.Size)).Append(")\n");
        }

        return sb.ToString().TrimEnd('\n');
    }

    private static string HtmlLines(OrderNotificationView order)
    {
        var sb = new StringBuilder("<ul>");
        foreach (var line in order.Lines)
        {
            sb.Append("<li>").Append(line.Quantity.ToString(CultureInfo.InvariantCulture)).Append(" x ").Append(WebUtility.HtmlEncode(Clean(line.Name)))
                .Append(" (").Append(WebUtility.HtmlEncode(Clean(line.Colour))).Append(", ").Append(WebUtility.HtmlEncode(Clean(line.Size))).Append(")</li>");
        }

        return sb.Append("</ul>").ToString();
    }

    private static string Clean(string value) => new(value.Where(c => !char.IsControl(c)).ToArray());

    private static string Cap(string value, int max) => value.Length <= max ? value : value[..max];

    [GeneratedRegex(@"\{\{(\w+)\}\}", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Placeholder();
}

/// <summary>
/// Renders from the newest active template for the kind and channel (templates are data, CHN-9). When none exists yet it falls back to the
/// built-in wording, so a missing row never stops a customer message.
/// </summary>
public sealed class TemplateMessageRenderer(INotificationsDbContext dbContext) : IMessageRenderer
{
    public const string DefaultLanguage = "EN";

    public async Task<RenderedMessage> RenderAsync(NotificationKind kind, NotificationChannel channel, OrderNotificationView order, CancellationToken cancellationToken)
    {
        var template = await dbContext.Templates
            .AsNoTracking()
            .Where(t => t.Kind == kind && t.Channel == channel && t.Language == DefaultLanguage && t.IsActive)
            .OrderByDescending(t => t.Version)
            .FirstOrDefaultAsync(cancellationToken);

        return template is null ? PlainMessageRenderer.RenderPlain(kind, channel, order) : TemplateEngine.Render(template, order);
    }
}
