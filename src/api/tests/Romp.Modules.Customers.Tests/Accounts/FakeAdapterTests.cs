using Microsoft.Extensions.Options;
using Romp.BuildingBlocks.Email;
using Romp.Modules.Customers.Infrastructure.Accounts;

namespace Romp.Modules.Customers.Tests.Accounts;

public sealed class EmailSenderTests
{
    [Fact]
    [Trait("Spec", "ACT-27")]
    public async Task FakeSender_RecordsEveryMessage()
    {
        var sender = new FakeEmailSender();
        var message = new EmailMessage("a@example.com", "Verify your email", "plain body", "<p>html body</p>");

        await sender.SendAsync(message, CancellationToken.None);

        var sent = Assert.Single(sender.Sent);
        Assert.Equal("a@example.com", sent.To);
        Assert.Equal("Verify your email", sent.Subject);
    }

    [Fact]
    [Trait("Spec", "ACT-27")]
    public void SmtpMessage_HasFromToSubjectAndBothBodies()
    {
        var options = new SmtpEmailOptions { Host = "localhost", FromAddress = "no-reply@romp.pk", FromName = "Romp" };
        var message = new EmailMessage("a@example.com", "Hello", "plain", "<p>html</p>");

        var mime = SmtpMessageFactory.Create(message, options);

        Assert.Equal("no-reply@romp.pk", mime.From.Mailboxes.Single().Address);
        Assert.Equal("Romp", mime.From.Mailboxes.Single().Name);
        Assert.Equal("a@example.com", mime.To.Mailboxes.Single().Address);
        Assert.Equal("Hello", mime.Subject);
        Assert.Equal("plain", mime.TextBody);
        Assert.Equal("<p>html</p>", mime.HtmlBody);
    }

    [Theory]
    [Trait("Spec", "ACT-27")]
    [InlineData("a@example.com\r\nBcc: victim@example.com")]
    [InlineData("not-an-address")]
    [InlineData("")]
    public void SmtpMessage_RefusesARecipientThatCouldInjectHeaders(string to)
    {
        var options = new SmtpEmailOptions { Host = "localhost", FromAddress = "no-reply@romp.pk", FromName = "Romp" };

        Assert.ThrowsAny<Exception>(() => SmtpMessageFactory.Create(new EmailMessage(to, "Hi", "plain", null), options));
    }

    [Fact]
    [Trait("Spec", "ACT-27")]
    public void SmtpMessage_StripsLineBreaksFromTheSubject()
    {
        var options = new SmtpEmailOptions { Host = "localhost", FromAddress = "no-reply@romp.pk", FromName = "Romp" };

        var mime = SmtpMessageFactory.Create(new EmailMessage("a@example.com", "Hi\r\nBcc: x@example.com", "plain", null), options);

        Assert.NotNull(mime.Subject);
        Assert.DoesNotContain("\n", mime.Subject, StringComparison.Ordinal);
        Assert.DoesNotContain("\r", mime.Subject, StringComparison.Ordinal);
    }

    [Theory]
    [Trait("Spec", "ACT-27")]
    [InlineData("", 25, "no-reply@romp.pk", false)]
    [InlineData("localhost", 0, "no-reply@romp.pk", false)]
    [InlineData("localhost", 1025, "", false)]
    [InlineData("localhost", 1025, "no-reply@romp.pk", true)]
    public void Options_AreValidatedAtStartUp(string host, int port, string from, bool valid)
    {
        var result = new SmtpEmailOptionsValidator().Validate(null, new SmtpEmailOptions { Host = host, Port = port, FromAddress = from, FromName = "Romp" });

        Assert.Equal(valid, result.Succeeded);
    }
}
