using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using Romp.BuildingBlocks.Web.Captcha;

namespace Romp.BuildingBlocks.Web.Tests.Captcha;

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
