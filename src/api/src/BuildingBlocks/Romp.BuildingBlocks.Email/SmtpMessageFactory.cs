using MimeKit;

namespace Romp.BuildingBlocks.Email;

/// <summary>Builds the MIME message. Refuses a recipient that could inject headers and flattens line breaks in the subject.</summary>
public static class SmtpMessageFactory
{
    private const int MaxAddressLength = 254;

    public static MimeMessage Create(EmailMessage message, SmtpEmailOptions options)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(options);

        var to = message.To;
        if (string.IsNullOrWhiteSpace(to) || to.Length > MaxAddressLength || to.Contains('\r') || to.Contains('\n')
            || !MailboxAddress.TryParse(to, out var recipient) || !recipient.Address.Contains('@', StringComparison.Ordinal))
        {
            throw new ArgumentException("The recipient is not a single valid email address.", nameof(message));
        }

        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(options.FromName, options.FromAddress));
        mime.To.Add(new MailboxAddress(string.Empty, recipient.Address));
        mime.Subject = message.Subject.Replace('\r', ' ').Replace('\n', ' ');

        var body = new BodyBuilder { TextBody = message.TextBody, HtmlBody = message.HtmlBody };
        mime.Body = body.ToMessageBody();

        if (message.Headers is not null)
        {
            foreach (var (header, value) in message.Headers)
            {
                mime.Headers.Add(header, value.Replace('\r', ' ').Replace('\n', ' '));
            }
        }

        return mime;
    }
}
