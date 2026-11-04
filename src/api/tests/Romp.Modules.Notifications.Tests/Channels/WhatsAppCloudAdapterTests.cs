using System.Net;
using System.Text.Json;
using Romp.Modules.Notifications.Application.Channels;
using Romp.Modules.Notifications.Application.Delivery;
using Romp.Modules.Notifications.Domain.Delivery;
using Romp.Modules.Notifications.Infrastructure.Channels;

namespace Romp.Modules.Notifications.Tests.Channels;

/// <summary>The real adapter against a stub HTTP handler: request shape, error mapping, template-only (CHN-13). No real provider is called.</summary>
public sealed class WhatsAppCloudAdapterTests
{
    private static readonly WhatsAppSettings Settings = new()
    {
        Enabled = true, PhoneNumberId = "1555000111", AccessToken = "tok-secret", AppSecret = "a", VerifyToken = "v", NumberSalt = "s",
    };

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return respond(request);
        }
    }

    private static (WhatsAppCloudSender Sender, StubHandler Handler) Make(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var handler = new StubHandler(respond);
        return (new WhatsAppCloudSender(new HttpClient(handler) { BaseAddress = new Uri(Settings.ApiBaseUrl) }, Settings), handler);
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };

    private static WhatsAppMessage CodRequest() => new(
        "+92 333 1234567", "text", [new WhatsAppButton("cod_confirm", "Confirm order"), new WhatsAppButton("cod_cancel", "Cancel order")],
        "romp_cod_confirmation", ["Ayesha", "RMP-0001", "PKR 1,500.00"]);

    [Fact]
    [Trait("Spec", "CHN-13")]
    public async Task SendAsync_PostsAnApprovedTemplateWithParametersAndButtons_AndReturnsTheProviderId()
    {
        var (sender, handler) = Make(_ => Json(HttpStatusCode.OK, """{"messaging_product":"whatsapp","messages":[{"id":"wamid.ABC"}]}"""));

        var id = await sender.SendAsync(CodRequest(), CancellationToken.None);

        Assert.Equal("wamid.ABC", id);
        Assert.Equal(HttpMethod.Post, handler.Request!.Method);
        Assert.Equal("https://graph.facebook.com/v21.0/1555000111/messages", handler.Request.RequestUri!.ToString());
        Assert.Equal("Bearer", handler.Request.Headers.Authorization!.Scheme);
        Assert.Equal("tok-secret", handler.Request.Headers.Authorization.Parameter);

        using var doc = JsonDocument.Parse(handler.Body!);
        var root = doc.RootElement;
        Assert.Equal("whatsapp", root.GetProperty("messaging_product").GetString());
        Assert.Equal("923331234567", root.GetProperty("to").GetString());
        Assert.Equal("template", root.GetProperty("type").GetString());
        var template = root.GetProperty("template");
        Assert.Equal("romp_cod_confirmation", template.GetProperty("name").GetString());
        Assert.Equal("en", template.GetProperty("language").GetProperty("code").GetString());
        var components = template.GetProperty("components");
        Assert.Equal(3, components.GetArrayLength());
        Assert.Equal("body", components[0].GetProperty("type").GetString());
        Assert.Equal("RMP-0001", components[0].GetProperty("parameters")[1].GetProperty("text").GetString());
        Assert.Equal("quick_reply", components[1].GetProperty("sub_type").GetString());
        Assert.Equal("0", components[1].GetProperty("index").GetString());
        Assert.Equal("cod_confirm", components[1].GetProperty("parameters")[0].GetProperty("payload").GetString());
        Assert.Equal("cod_cancel", components[2].GetProperty("parameters")[0].GetProperty("payload").GetString());
    }

    [Fact]
    [Trait("Spec", "CHN-13")]
    public async Task SendAsync_WithNoTemplate_IsRefusedAndNothingIsSent()
    {
        var (sender, handler) = Make(_ => Json(HttpStatusCode.OK, "{}"));

        var ex = await Assert.ThrowsAsync<ChannelSendException>(() => sender.SendAsync(new WhatsAppMessage("+923331234567", "free text", []), CancellationToken.None));

        Assert.Equal(ErrorCategory.Permanent, ex.Category);
        Assert.Null(handler.Request);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, ErrorCategory.Temporary)]
    [InlineData(HttpStatusCode.InternalServerError, ErrorCategory.Temporary)]
    [InlineData(HttpStatusCode.BadGateway, ErrorCategory.Temporary)]
    [InlineData(HttpStatusCode.RequestTimeout, ErrorCategory.Temporary)]
    [InlineData(HttpStatusCode.Unauthorized, ErrorCategory.Temporary)]
    [InlineData(HttpStatusCode.Forbidden, ErrorCategory.Temporary)]
    [InlineData(HttpStatusCode.BadRequest, ErrorCategory.Permanent)]
    [InlineData(HttpStatusCode.NotFound, ErrorCategory.Permanent)]
    [Trait("Spec", "CHN-13")]
    public async Task SendAsync_MapsProviderErrorsToTheCategoryTheWorkerUnderstands(HttpStatusCode status, ErrorCategory expected)
    {
        var (sender, _) = Make(_ => Json(status, """{"error":{"message":"to 923331234567 failed","code":131026}}"""));

        var ex = await Assert.ThrowsAsync<ChannelSendException>(() => sender.SendAsync(CodRequest(), CancellationToken.None));

        Assert.Equal(expected, ex.Category);
        Assert.DoesNotContain("923331234567", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("tok-secret", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Spec", "CHN-13")]
    public async Task SendAsync_WhenTheProviderCannotBeReached_IsTemporary()
    {
        var (sender, _) = Make(_ => throw new HttpRequestException("connection refused to 923331234567"));

        var ex = await Assert.ThrowsAsync<ChannelSendException>(() => sender.SendAsync(CodRequest(), CancellationToken.None));

        Assert.Equal(ErrorCategory.Temporary, ex.Category);
        Assert.DoesNotContain("923331234567", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Spec", "CHN-13")]
    public async Task SendAsync_WhenAcceptedButTheReplyIsUnreadable_DoesNotSendAgain()
    {
        var (sender, _) = Make(_ => Json(HttpStatusCode.OK, "not json"));

        Assert.Equal(string.Empty, await sender.SendAsync(CodRequest(), CancellationToken.None));
    }

    [Fact]
    [Trait("Spec", "CHN-13")]
    public async Task SendAsync_WithNoNumber_IsNoAddress()
    {
        var (sender, handler) = Make(_ => Json(HttpStatusCode.OK, "{}"));

        var ex = await Assert.ThrowsAsync<ChannelSendException>(
            () => sender.SendAsync(new WhatsAppMessage(string.Empty, "t", [], "romp_x", []), CancellationToken.None));

        Assert.Equal(ErrorCategory.NoAddress, ex.Category);
        Assert.Null(handler.Request);
    }

    [Theory]
    [InlineData(false, "Development", false)]
    [InlineData(false, "Testing", false)]
    [InlineData(false, "Production", true)]
    [InlineData(true, "Production", true)]
    [Trait("Spec", "CHN-8")]
    public void StartupCheck_RefusesTheFakeOutsideDevelopmentAndTesting_AndAnEnabledAdapterMissingSecrets(bool enabled, string environment, bool refused)
    {
        var settings = new WhatsAppSettings { Enabled = enabled };

        var problem = WhatsAppStartupCheck.Validate(settings, environment);

        Assert.Equal(refused, problem is not null);
    }

    [Fact]
    [Trait("Spec", "CHN-8")]
    public void StartupCheck_NamesTheMissingKeysButNeverAValue()
    {
        var settings = new WhatsAppSettings { Enabled = true, AccessToken = "tok-secret", PhoneNumberId = "1" };

        var problem = WhatsAppStartupCheck.Validate(settings, "Production");

        Assert.Contains("AppSecret", problem, StringComparison.Ordinal);
        Assert.Contains("VerifyToken", problem, StringComparison.Ordinal);
        Assert.Contains("NumberSalt", problem, StringComparison.Ordinal);
        Assert.DoesNotContain("AccessToken,", problem, StringComparison.Ordinal);
        Assert.DoesNotContain("tok-secret", problem, StringComparison.Ordinal);
        Assert.Null(WhatsAppStartupCheck.Validate(Settings, "Production"));
    }
}
