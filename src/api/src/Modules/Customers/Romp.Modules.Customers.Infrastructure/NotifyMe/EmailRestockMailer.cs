using System.Net;
using Microsoft.Extensions.Options;
using Romp.BuildingBlocks.Email;
using Romp.Modules.Customers.Application;
using Romp.Modules.Customers.Application.NotifyMe;

namespace Romp.Modules.Customers.Infrastructure.NotifyMe;

/// <summary>
/// Renders and sends the restock email through the shared <see cref="IEmailSender"/> (ADR 0022). It is a one-off answer to the shopper's
/// own request, not marketing: no promotions, no unsubscribe headers, and it says it is the only email about the request. When the address is
/// also a newsletter subscriber, a footer link unsubscribes it from the newsletter (signed token, never the address). The link points
/// at the storefront (<c>Customers:PublicBaseUrl</c>), never at a host taken from a request. Every interpolated value is HTML-encoded and
/// the subject has line breaks removed; a plain-text twin is always sent.
/// </summary>
public sealed class EmailRestockMailer(IEmailSender sender, IOptions<CustomersOptions> options) : IRestockMailer
{
    public Task SendAsync(string toEmail, RestockNotice notice, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notice);
        var baseUrl = options.Value.PublicBaseUrl.TrimEnd('/');
        var link = baseUrl + (notice.PagePath.StartsWith('/') ? notice.PagePath : "/" + notice.PagePath);
        var subject = StripLineBreaks($"{notice.ProductName} is back in stock");
        var detail = $"{notice.ProductName}, {notice.ColourName}, size {notice.SizeName}";

        const string Intro = "Good news. You asked us to tell you when this item was back:";
        const string Outro = "Stock is limited, so it may sell out again. This is the only email about this request.";

        var text = $"{Intro}\n\n{detail}\n{link}\n\n{Outro}\n";
        var html =
            $"<p>{WebUtility.HtmlEncode(Intro)}</p>"
            + $"<p><strong>{WebUtility.HtmlEncode(detail)}</strong></p>"
            + $"<p><a href=\"{WebUtility.HtmlEncode(link)}\">View it in the shop</a></p>"
            + $"<p>{WebUtility.HtmlEncode(Outro)}</p>";

        // Only a current newsletter subscriber gets the link; it unsubscribes from the newsletter, not from this request.
        if (notice.UnsubscribeToken is { Length: > 0 } token)
        {
            const string Footer = "You also receive the Romp newsletter. You can unsubscribe from it at any time:";
            var page = $"{baseUrl}/newsletter/unsubscribe?token={Uri.EscapeDataString(token)}";
            text += $"\n{Footer}\n{page}\n";
            html += $"<p>{WebUtility.HtmlEncode(Footer)} <a href=\"{WebUtility.HtmlEncode(page)}\">Unsubscribe</a></p>";
        }

        return sender.SendAsync(new EmailMessage(toEmail, subject, text, html), cancellationToken);
    }

    private static string StripLineBreaks(string value) => value.Replace("\r", string.Empty, StringComparison.Ordinal).Replace("\n", string.Empty, StringComparison.Ordinal);
}
