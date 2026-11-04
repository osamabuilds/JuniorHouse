using System.Net;
using Microsoft.Extensions.Options;
using Romp.BuildingBlocks.Email;
using Romp.Modules.Customers.Application;
using Romp.Modules.Customers.Application.Newsletter;

namespace Romp.Modules.Customers.Infrastructure.Newsletter;

/// <summary>
/// Renders and sends the newsletter welcome through the shared <see cref="IEmailSender"/> (ADR 0022). This is marketing mail, so it carries
/// <c>List-Unsubscribe</c> and <c>List-Unsubscribe-Post</c> (one-click) and a visible unsubscribe link (NLS-3). Links point at the storefront
/// (<c>Customers:PublicBaseUrl</c>), never at a host taken from a request, and hold the signed token, never the address. Every
/// interpolated value is HTML-encoded and header values are stripped of line breaks; a plain-text twin is always sent.
/// </summary>
public sealed class EmailNewsletterMailer(IEmailSender sender, IOptions<CustomersOptions> options) : INewsletterMailer
{
    public Task SendWelcomeAsync(string toEmail, string unsubscribeToken, CancellationToken cancellationToken)
    {
        var baseUrl = options.Value.PublicBaseUrl.TrimEnd('/');
        var token = Uri.EscapeDataString(unsubscribeToken);
        var page = $"{baseUrl}/newsletter/unsubscribe?token={token}";
        var oneClick = $"{baseUrl}/customer-api/newsletter/unsubscribe?token={token}";

        const string Subject = "Welcome to the Romp newsletter";
        const string Intro = "Thank you for subscribing. We will send you news about new arrivals and offers. We will never sell or share your email address.";
        const string Footer = "You are receiving this because this address was subscribed to the Romp newsletter. You can unsubscribe at any time:";

        var text = $"{Intro}\n\n{Footer}\n{page}\n";
        var html =
            $"<p>{WebUtility.HtmlEncode(Intro)}</p>"
            + $"<p>{WebUtility.HtmlEncode(Footer)} <a href=\"{WebUtility.HtmlEncode(page)}\">Unsubscribe</a></p>";

        var headers = new Dictionary<string, string>
        {
            ["List-Unsubscribe"] = $"<{StripLineBreaks(oneClick)}>",
            ["List-Unsubscribe-Post"] = "List-Unsubscribe=One-Click",
        };
        return sender.SendAsync(new EmailMessage(toEmail, Subject, text, html, headers), cancellationToken);
    }

    private static string StripLineBreaks(string value) => value.Replace("\r", string.Empty, StringComparison.Ordinal).Replace("\n", string.Empty, StringComparison.Ordinal);
}
