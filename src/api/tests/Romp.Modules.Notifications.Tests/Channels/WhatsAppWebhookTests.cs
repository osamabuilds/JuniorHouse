using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Romp.BuildingBlocks.Application;
using Romp.Modules.Notifications.Application.Channels;
using Romp.Modules.Notifications.Domain.Channels;
using Romp.Modules.Notifications.Tests.Support;

namespace Romp.Modules.Notifications.Tests.Channels;

/// <summary>The inbound webhook (CHN-11, SEC-10): signature first, stored once, routed by type, refused events never acted on.</summary>
public sealed class WhatsAppWebhookTests(NotificationsPostgres postgres) : IClassFixture<NotificationsPostgres>, IAsyncLifetime
{
    private const string Secret = "app-secret-for-tests";

    private static readonly DateTimeOffset Now = new(2026, 11, 3, 9, 0, 0, TimeSpan.Zero);

    public async Task InitializeAsync()
    {
        await postgres.ResetAsync();
        await using var db = postgres.NewContext();
        await db.Database.ExecuteSqlRawAsync("TRUNCATE \"NTFY\".\"NTFY_WBHK\" CASCADE");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static string Reply(string id = "wamid.IN1", string replyTo = "wamid.OUT1", string button = "cod_confirm") =>
        """{"object":"whatsapp_business_account","entry":[{"changes":[{"value":{"messages":[{"from":"923331234567","id":"@ID@","type":"interactive","context":{"id":"@REPLY@"},"interactive":{"type":"button_reply","button_reply":{"id":"@BTN@","title":"Confirm"}}}]}}]}]}"""
            .Replace("@ID@", id, StringComparison.Ordinal).Replace("@REPLY@", replyTo, StringComparison.Ordinal).Replace("@BTN@", button, StringComparison.Ordinal);

    private static byte[] Bytes(string json) => Encoding.UTF8.GetBytes(json);

    private static string Sign(byte[] body) => WhatsAppSignature.Compute(Secret, body);

    private sealed class RecordingHandler(WhatsAppEventType handles) : IWhatsAppInboundHandler
    {
        public WhatsAppEventType EventType => handles;

        public List<WhatsAppInboundEvent> Seen { get; } = [];

        public Task<long?> HandleAsync(WhatsAppInboundEvent inbound, CancellationToken cancellationToken)
        {
            Seen.Add(inbound);
            return Task.FromResult<long?>(null);
        }
    }

    private sealed class CapturingLogger : ILogger<WhatsAppWebhookProcessor>
    {
        public List<(int Id, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((eventId.Id, formatter(state, exception)));
    }

    private (WhatsAppWebhookProcessor Processor, Romp.Modules.Notifications.Infrastructure.Persistence.NtfyDbContext Db, CapturingLogger Log) Make(params IWhatsAppInboundHandler[] handlers)
    {
        var db = postgres.NewContext();
        var log = new CapturingLogger();
        return (new WhatsAppWebhookProcessor(db, new WhatsAppSettings { AppSecret = Secret }, handlers, new TestClock(Now), log), db, log);
    }

    [Fact]
    [Trait("Spec", "CHN-11")]
    public async Task HandleAsync_WithAValidSignature_StoresTheEventAndHandsItToItsHandler()
    {
        var handler = new RecordingHandler(WhatsAppEventType.ButtonReply);
        var (processor, db, _) = Make(handler);
        await using var _1 = db;
        var body = Bytes(Reply());

        var outcome = await processor.HandleAsync(body, Sign(body), "1.2.3.4", CancellationToken.None);

        Assert.Equal(WebhookOutcome.Accepted, outcome);
        var seen = Assert.Single(handler.Seen);
        Assert.Equal("wamid.OUT1", seen.ProviderMessageId);
        Assert.Equal("cod_confirm", seen.Payload);
        await using var check = postgres.NewContext();
        var stored = await check.WhatsAppEvents.SingleAsync();
        Assert.True(stored.SignatureVerified);
        Assert.NotNull(stored.ProcessedAt);
        Assert.Equal(WhatsAppEventType.ButtonReply, stored.Type);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("sha256=deadbeef")]
    [Trait("Spec", "CHN-11")]
    public async Task HandleAsync_WithAMissingOrWrongSignature_IsRefusedStoredAsUnverifiedAndLoggedAsASecurityEvent(string? header)
    {
        var handler = new RecordingHandler(WhatsAppEventType.ButtonReply);
        var (processor, db, log) = Make(handler);
        await using var _1 = db;

        var outcome = await processor.HandleAsync(Bytes(Reply()), header, "1.2.3.4", CancellationToken.None);

        Assert.Equal(WebhookOutcome.InvalidSignature, outcome);
        Assert.Empty(handler.Seen);
        Assert.Contains(log.Entries, e => e.Id == SecurityEvents.WebhookSignatureRejectedId);
        await using var check = postgres.NewContext();
        var stored = await check.WhatsAppEvents.SingleAsync();
        Assert.False(stored.SignatureVerified);
        Assert.Null(stored.Type);
        Assert.Null(stored.ProcessedAt);
    }

    [Fact]
    [Trait("Spec", "CHN-11")]
    public async Task HandleAsync_WhenTheBodyWasChangedAfterSigning_IsRefused()
    {
        var (processor, db, _) = Make();
        await using var _1 = db;
        var signed = Bytes(Reply(button: "cod_confirm"));

        var outcome = await processor.HandleAsync(Bytes(Reply(button: "cod_cancel")), Sign(signed), "1.2.3.4", CancellationToken.None);

        Assert.Equal(WebhookOutcome.InvalidSignature, outcome);
    }

    [Fact]
    [Trait("Spec", "CHN-11")]
    public async Task HandleAsync_WithNoAppSecretConfigured_RefusesEverything()
    {
        await using var db = postgres.NewContext();
        var processor = new WhatsAppWebhookProcessor(db, new WhatsAppSettings(), [], new TestClock(Now), new CapturingLogger());
        var body = Bytes(Reply());

        var outcome = await processor.HandleAsync(body, WhatsAppSignature.Compute(string.Empty, body), "1.2.3.4", CancellationToken.None);

        Assert.Equal(WebhookOutcome.InvalidSignature, outcome);
    }

    [Fact]
    [Trait("Spec", "CHN-11")]
    public async Task HandleAsync_WhenTheSameEventArrivesAgain_IsHandledOnlyOnce()
    {
        var handler = new RecordingHandler(WhatsAppEventType.ButtonReply);
        var body = Bytes(Reply());

        for (var i = 0; i < 3; i++)
        {
            var (processor, db, _) = Make(handler);
            await using var _1 = db;
            Assert.Equal(WebhookOutcome.Accepted, await processor.HandleAsync(body, Sign(body), "1.2.3.4", CancellationToken.None));
        }

        Assert.Single(handler.Seen);
        await using var check = postgres.NewContext();
        Assert.Equal(1, await check.WhatsAppEvents.CountAsync());
    }

    [Fact]
    [Trait("Spec", "CHN-11")]
    public async Task HandleAsync_WhenTheBodyIsOverTheCap_IsRefusedBeforeAnythingIsRead()
    {
        await using var db = postgres.NewContext();
        var processor = new WhatsAppWebhookProcessor(db, new WhatsAppSettings { AppSecret = Secret, MaxBodyBytes = 100 }, [], new TestClock(Now), new CapturingLogger());
        var body = Bytes(new string('x', 101));

        Assert.Equal(WebhookOutcome.TooLarge, await processor.HandleAsync(body, Sign(body), "1.2.3.4", CancellationToken.None));
        Assert.Equal(0, await db.WhatsAppEvents.CountAsync());
    }

    [Fact]
    [Trait("Spec", "CHN-11")]
    public async Task HandleAsync_WithASignedButUnreadableBody_IsABadPayload()
    {
        var (processor, db, _) = Make();
        await using var _1 = db;
        var body = Bytes("not json");

        Assert.Equal(WebhookOutcome.BadPayload, await processor.HandleAsync(body, Sign(body), "1.2.3.4", CancellationToken.None));
    }

    [Fact]
    [Trait("Spec", "CHN-11")]
    public async Task HandleAsync_WhenAHandlerFails_TheEventCanBeRetriedByTheProvider()
    {
        var failing = new FailingOnceHandler();
        var body = Bytes(Reply());
        var (first, db1, _) = Make(failing);
        await using var _1 = db1;
        await Assert.ThrowsAsync<InvalidOperationException>(() => first.HandleAsync(body, Sign(body), "1.2.3.4", CancellationToken.None));

        var (second, db2, _) = Make(failing);
        await using var _2 = db2;
        var outcome = await second.HandleAsync(body, Sign(body), "1.2.3.4", CancellationToken.None);

        Assert.Equal(WebhookOutcome.Accepted, outcome);
        Assert.Equal(2, failing.Calls);
        await using var check = postgres.NewContext();
        Assert.NotNull((await check.WhatsAppEvents.SingleAsync()).ProcessedAt);
    }

    private sealed class FailingOnceHandler : IWhatsAppInboundHandler
    {
        public WhatsAppEventType EventType => WhatsAppEventType.ButtonReply;

        public int Calls { get; private set; }

        public Task<long?> HandleAsync(WhatsAppInboundEvent inbound, CancellationToken cancellationToken)
        {
            Calls++;
            return Calls == 1 ? throw new InvalidOperationException("down") : Task.FromResult<long?>(null);
        }
    }
}

public sealed class WhatsAppPayloadParserTests
{
    [Fact]
    [Trait("Spec", "CHN-11")]
    public void Parse_ReadsButtonRepliesStopAndReceiptsAndIgnoresTheRest()
    {
        const string json = """
            {"entry":[{"changes":[{"value":{
              "messages":[
                {"from":"923331234567","id":"m1","type":"interactive","context":{"id":"out1"},"interactive":{"type":"button_reply","button_reply":{"id":"cod_cancel"}}},
                {"from":"923331234567","id":"m2","type":"text","text":{"body":" stop "}},
                {"from":"923331234567","id":"m3","type":"text","text":{"body":"hello there"}},
                {"from":"923331234567","id":"m4","type":"image"}],
              "statuses":[{"id":"out1","status":"delivered","recipient_id":"923331234567"}]}}]}]}
            """;

        var events = WhatsAppPayloadParser.Parse(Encoding.UTF8.GetBytes(json));

        Assert.Equal(3, events.Count);
        Assert.Contains(events, e => e.Type == WhatsAppEventType.ButtonReply && e.ProviderMessageId == "out1" && e.Payload == "cod_cancel" && e.SenderNumber == "+923331234567");
        Assert.Contains(events, e => e.Type == WhatsAppEventType.Stop && e.EventId == "m2");
        Assert.Contains(events, e => e.Type == WhatsAppEventType.Receipt && e.EventId == "out1:delivered" && e.Payload == "delivered");
    }

    [Fact]
    [Trait("Spec", "CHN-11")]
    public void Parse_OfAnUnexpectedShape_ReturnsNothingInsteadOfThrowing()
    {
        Assert.Empty(WhatsAppPayloadParser.Parse(Encoding.UTF8.GetBytes("""{"entry":"x"}""")));
        Assert.Empty(WhatsAppPayloadParser.Parse(Encoding.UTF8.GetBytes("[]")));
    }

    [Fact]
    [Trait("Spec", "CHN-11")]
    public void IsValid_ComparesTheWholeSignatureAndIgnoresCase()
    {
        var body = Encoding.UTF8.GetBytes("{}");
        var good = WhatsAppSignature.Compute("s", body);

        Assert.True(WhatsAppSignature.IsValid("s", body, good.ToUpperInvariant().Replace("SHA256=", "sha256=", StringComparison.Ordinal)));
        Assert.False(WhatsAppSignature.IsValid("s", body, good[..^1]));
        Assert.False(WhatsAppSignature.IsValid("other", body, good));
    }
}
