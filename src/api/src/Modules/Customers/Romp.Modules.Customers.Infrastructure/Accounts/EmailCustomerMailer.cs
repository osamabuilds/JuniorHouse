using System.Net;
using Microsoft.Extensions.Options;
using Romp.BuildingBlocks.Email;
using Romp.Modules.Customers.Application;
using Romp.Modules.Customers.Application.Accounts;
using Romp.Modules.Customers.Domain.Accounts;

namespace Romp.Modules.Customers.Infrastructure.Accounts;

/// <summary>
/// Renders and sends the two link emails through the shared <see cref="IEmailSender"/> (ADR 0022). Links point at the storefront
/// (<c>Customers:PublicBaseUrl</c>), never at a host taken from a request. Every interpolated value is HTML-encoded; a plain-text twin is
/// always sent. These are transactional messages: no marketing content and no unsubscribe header.
/// </summary>
public sealed class EmailCustomerMailer(IEmailSender sender, IOptions<CustomersOptions> options) : ICustomerMailer
{
    public Task SendVerificationAsync(string toEmail, string firstName, SecretString token, CancellationToken cancellationToken)
    {
        var link = Link("account/verify-email", token);
        return Send(
            toEmail,
            "Confirm your email address",
            firstName,
            "Please confirm your email address so we can reach you about your orders.",
            "Confirm my email",
            link,
            "This link works once and expires in 24 hours. If you did not create an account, you can ignore this email.",
            cancellationToken);
    }

    public Task SendPasswordResetAsync(string toEmail, string firstName, SecretString token, CancellationToken cancellationToken)
    {
        var link = Link("account/reset-password", token);
        return Send(
            toEmail,
            "Set a new password",
            firstName,
            "We received a request to set a new password for your account.",
            "Set a new password",
            link,
            "This link works once and expires in 60 minutes. If you did not ask for it, you can ignore this email; your password has not changed.",
            cancellationToken);
    }

    private string Link(string path, SecretString token) =>
        $"{options.Value.PublicBaseUrl.TrimEnd('/')}/{path}?token={Uri.EscapeDataString(token.Reveal())}";

    private Task Send(string to, string subject, string firstName, string intro, string action, string link, string footer, CancellationToken cancellationToken)
    {
        var name = WebUtility.HtmlEncode(firstName);
        var text = $"Hello {firstName},\n\n{intro}\n\n{action}: {link}\n\n{footer}\n";
        var html =
            $"<p>Hello {name},</p><p>{WebUtility.HtmlEncode(intro)}</p>"
            + $"<p><a href=\"{WebUtility.HtmlEncode(link)}\">{WebUtility.HtmlEncode(action)}</a></p>"
            + $"<p>{WebUtility.HtmlEncode(footer)}</p>";
        return sender.SendAsync(new EmailMessage(to, subject, text, html), cancellationToken);
    }
}
