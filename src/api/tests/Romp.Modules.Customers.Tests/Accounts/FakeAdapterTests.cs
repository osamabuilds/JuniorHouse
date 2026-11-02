using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using Romp.BuildingBlocks.Email;
using Romp.Modules.Customers.Infrastructure.Accounts;

namespace Romp.Modules.Customers.Tests.Accounts;

public sealed class FakeCaptchaVerifierTests
{
    [Fact]
    [Trait("Spec", "ACT-9")]
    public async Task Verify_AcceptsOnlyTheConfiguredPassToken()
    {
        var verifier = new FakeCaptchaVerifier();

        Assert.True(await verifier.VerifyAsync(FakeCaptchaVerifier.PassToken, "127.0.0.1", CancellationToken.None));
        Assert.False(await verifier.VerifyAsync("anything-else", "127.0.0.1", CancellationToken.None));
        Assert.False(await verifier.VerifyAsync(null, null, CancellationToken.None));
        Assert.False(await verifier.VerifyAsync(string.Empty, null, CancellationToken.None));
    }
}

public sealed class TurnstileCaptchaVerifierTests
{
    private static TurnstileCaptchaVerifier Verifier(HttpMessageHandler handler, string secret = "s3cret") =>
        new(new HttpClient(handler), Options.Create(new TurnstileOptions { SecretKey = secret }));

    [Fact]
    [Trait("Spec", "ACT-9")]
    public async Task Verify_PostsSecretTokenAndIp_AndAcceptsASuccess()
    {
        var handler = new StubHandler(HttpStatusCode.OK, """{"success":true}""");

        var ok = await Verifier(handler).VerifyAsync("token-123", "203.0.113.7", CancellationToken.None);

        Assert.True(ok);
        Assert.Equal(TurnstileCaptchaVerifier.VerifyUrl, handler.RequestUri!.ToString());
        Assert.Contains("secret=s3cret", handler.Body, StringComparison.Ordinal);
        Assert.Contains("response=token-123", handler.Body, StringComparison.Ordinal);
        Assert.Contains("remoteip=203.0.113.7", handler.Body, StringComparison.Ordinal);
    }

    [Theory]
    [Trait("Spec", "ACT-9")]
    [InlineData(HttpStatusCode.OK, """{"success":false,"error-codes":["invalid-input-response"]}""")]
    [InlineData(HttpStatusCode.OK, "not json")]
    [InlineData(HttpStatusCode.InternalServerError, """{"success":true}""")]
    public async Task Verify_RefusesAFailureAnErrorOrGarbage(HttpStatusCode status, string body)
    {
        Assert.False(await Verifier(new StubHandler(status, body)).VerifyAsync("token", null, CancellationToken.None));
    }

    [Theory]
    [Trait("Spec", "ACT-9")]
    [InlineData(null)]
    [InlineData("")]
    public async Task Verify_WithoutAToken_NeverCallsTheService(string? token)
    {
        var handler = new StubHandler(HttpStatusCode.OK, """{"success":true}""");

        Assert.False(await Verifier(handler).VerifyAsync(token, null, CancellationToken.None));
        Assert.Null(handler.RequestUri);
    }

    [Fact]
    [Trait("Spec", "ACT-9")]
    public async Task Verify_WhenTheServiceIsUnreachable_FailsClosed()
    {
        Assert.False(await Verifier(new ThrowingHandler()).VerifyAsync("token", null, CancellationToken.None));
    }

    private sealed class StubHandler(HttpStatusCode status, string json) : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }

        public string Body { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            Body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        }
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("offline");
    }
}

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
