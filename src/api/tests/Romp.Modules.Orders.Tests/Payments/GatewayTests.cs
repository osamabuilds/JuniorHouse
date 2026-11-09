using System.Text;
using Microsoft.Extensions.Options;
using Romp.Modules.Orders.Application.Checkout;
using Romp.Modules.Orders.Application.Payments;
using Romp.Modules.Orders.Domain.Payments;
using Romp.Modules.Orders.Infrastructure.Payments;
using Romp.Modules.Orders.Tests.Support;

namespace Romp.Modules.Orders.Tests.Payments;

/// <summary>The circuit breaker (SEC-21, PAY-7), the resilient wrapper, and the fake gateway's signatures, replay window and idempotence.</summary>
public sealed class GatewayTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 9, 10, 0, 0, TimeSpan.Zero);

    private static OrdersOptions Settings() => new() { GatewayProvider = "fake", FakeGatewayWebhookSecret = "test-only-fake-gateway-secret-0123456789", GatewayTimeoutSeconds = 1 };

    private static FakeGateway NewFake(FixedTime time, OrdersOptions? settings = null) => new(Options.Create(settings ?? Settings()), time);

    private static CreateSessionRequest Request(string attempt = "RMP-1-A1", decimal amount = 3150m) => new("RMP-1", attempt, amount, "CARD", "/return");

    // ---- circuit breaker ------------------------------------------------------------------------------------------------------------

    [Fact]
    [Trait("Spec", "PAY-7")]
    public void TheCircuit_OpensAfterFiveFailuresInAMinute_AndRefusesCalls()
    {
        var time = new FixedTime(Start);
        var circuit = new GatewayCircuit(time, 5, TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(30));

        for (var i = 0; i < 4; i++)
        {
            circuit.RecordFailure();
            time.Now += TimeSpan.FromSeconds(5);
        }

        Assert.False(circuit.IsOpen);
        circuit.RecordFailure();

        Assert.True(circuit.IsOpen);
        Assert.False(circuit.TryAcquire());
    }

    [Fact]
    [Trait("Spec", "PAY-7")]
    public void FailuresSpreadOverMoreThanAMinute_DoNotOpenTheCircuit()
    {
        var time = new FixedTime(Start);
        var circuit = new GatewayCircuit(time, 5, TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(30));

        for (var i = 0; i < 10; i++)
        {
            circuit.RecordFailure();
            time.Now += TimeSpan.FromSeconds(20);
        }

        Assert.False(circuit.IsOpen);
    }

    [Fact]
    [Trait("Spec", "PAY-7")]
    public void AfterThirtySeconds_OneTrialIsAllowed_AndASuccessClosesTheCircuit()
    {
        var time = new FixedTime(Start);
        var circuit = new GatewayCircuit(time, 2, TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(30));
        circuit.RecordFailure();
        circuit.RecordFailure();
        Assert.True(circuit.IsOpen);

        time.Now += TimeSpan.FromSeconds(31);

        Assert.False(circuit.IsOpen);
        Assert.True(circuit.TryAcquire());
        Assert.False(circuit.TryAcquire());
        circuit.RecordSuccess();
        Assert.True(circuit.TryAcquire());
        Assert.False(circuit.IsOpen);
    }

    [Fact]
    [Trait("Spec", "PAY-7")]
    public void AFailedTrial_OpensTheCircuitAgainForAnotherThirtySeconds()
    {
        var time = new FixedTime(Start);
        var circuit = new GatewayCircuit(time, 2, TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(30));
        circuit.RecordFailure();
        circuit.RecordFailure();
        time.Now += TimeSpan.FromSeconds(31);
        Assert.True(circuit.TryAcquire());

        circuit.RecordFailure();

        Assert.True(circuit.IsOpen);
        time.Now += TimeSpan.FromSeconds(29);
        Assert.True(circuit.IsOpen);
        time.Now += TimeSpan.FromSeconds(2);
        Assert.False(circuit.IsOpen);
    }

    [Fact]
    public async Task ARunOfConcurrentTrials_LetsExactlyOneCallerThrough()
    {
        var time = new FixedTime(Start);
        var circuit = new GatewayCircuit(time, 1, TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(30));
        circuit.RecordFailure();
        time.Now += TimeSpan.FromSeconds(31);

        var results = await Task.WhenAll(Enumerable.Range(0, 100).Select(_ => Task.Run(circuit.TryAcquire)));

        Assert.Equal(1, results.Count(r => r));
    }

    // ---- the resilient wrapper --------------------------------------------------------------------------------------------------

    [Fact]
    [Trait("Spec", "PAY-7")]
    public async Task WhenTheGatewayIsDown_TheWrapperCountsFailures_ThenStopsCallingIt()
    {
        var time = new FixedTime(Start);
        var settings = Settings();
        var fake = NewFake(time, settings) ;
        fake.Mode = FakeGatewayMode.Down;
        var circuit = new GatewayCircuit(time, settings.CircuitFailureThreshold, TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(30));
        var gateway = new ResilientPaymentGateway(fake, circuit, Options.Create(settings));
        Assert.True(gateway.IsAvailable);

        for (var i = 0; i < 5; i++)
        {
            await Assert.ThrowsAsync<GatewayUnavailableException>(() => gateway.CreateSessionAsync(Request($"a{i}"), CancellationToken.None));
        }

        Assert.False(gateway.IsAvailable);
        Assert.True(gateway.IsPaused);
        var callsBefore = fake.SessionCreateCalls;
        await Assert.ThrowsAsync<GatewayUnavailableException>(() => gateway.CreateSessionAsync(Request("later"), CancellationToken.None));
        Assert.Equal(callsBefore, fake.SessionCreateCalls);

        // The gateway recovers; after the open period one trial succeeds and the circuit closes.
        fake.Mode = FakeGatewayMode.Normal;
        time.Now += TimeSpan.FromSeconds(31);
        var session = await gateway.CreateSessionAsync(Request("back"), CancellationToken.None);
        Assert.NotEmpty(session.GatewayRef);
        Assert.True(gateway.IsAvailable);
    }

    [Fact]
    public async Task ASlowGateway_TimesOut_AndCountsAsAFailure()
    {
        var settings = Settings();
        var gateway = new ResilientPaymentGateway(new SlowGateway(), new GatewayCircuit(TimeProvider.System, 1, TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(30)), Options.Create(settings));

        await Assert.ThrowsAsync<GatewayUnavailableException>(() => gateway.GetStatusAsync("x", CancellationToken.None));

        Assert.False(gateway.IsAvailable);
    }

    [Fact]
    public async Task ACallerThatGivesUp_IsNotAGatewayFailure()
    {
        var gateway = new ResilientPaymentGateway(new SlowGateway(), new GatewayCircuit(TimeProvider.System, 1, TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(30)), Options.Create(Settings()));
        using var cancelled = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => gateway.GetStatusAsync("x", cancelled.Token));

        Assert.True(gateway.IsAvailable);
    }

    [Fact]
    [Trait("Spec", "PAY-14")]
    public async Task WithNoGatewaySwitchedOn_EveryCallSaysSo_AndOnlineIsNeverAvailable()
    {
        var gateway = new ResilientPaymentGateway(new NoGateway(), new GatewayCircuit(TimeProvider.System, 5, TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(30)), Options.Create(Settings()));

        Assert.False(gateway.IsAvailable);
        Assert.False(gateway.IsPaused);
        await Assert.ThrowsAsync<GatewayUnavailableException>(() => gateway.CreateSessionAsync(Request(), CancellationToken.None));
        Assert.False(gateway.VerifyCallback([], new Dictionary<string, string>(), Start).Verified);
    }

    private sealed class SlowGateway : IPaymentGateway
    {
        public string Provider => "slow";

        public bool IsConfigured => true;

        public async Task<GatewaySession> CreateSessionAsync(CreateSessionRequest request, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return new GatewaySession("x", "/x");
        }

        public CallbackVerification VerifyCallback(byte[] rawBody, IReadOnlyDictionary<string, string> headers, DateTimeOffset now) => CallbackVerification.Reject(CallbackRejection.BadSignature);

        public async Task<GatewayStatus> GetStatusAsync(string gatewayRef, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return new GatewayStatus(GatewayStatusKind.Unknown, null);
        }

        public Task<RefundResult> RefundAsync(RefundRequest request, CancellationToken cancellationToken) => Task.FromResult(new RefundResult(false, null));
    }

    // ---- the fake gateway ------------------------------------------------------------------------------------------------------------

    [Fact]
    [Trait("Spec", "PAY-3")]
    public async Task AskingForTheSameAttemptTwice_GivesTheSameSession()
    {
        var fake = NewFake(new FixedTime(Start));

        var first = await fake.CreateSessionAsync(Request(), CancellationToken.None);
        var second = await fake.CreateSessionAsync(Request(), CancellationToken.None);
        var other = await fake.CreateSessionAsync(Request("RMP-1-A2"), CancellationToken.None);

        Assert.Equal(first, second);
        Assert.NotEqual(first.GatewayRef, other.GatewayRef);
        Assert.Equal(2, fake.Sessions.Count);
    }

    [Fact]
    [Trait("Spec", "SEC-10")]
    public async Task AValidlySignedCallback_IsAccepted_AndReadBack()
    {
        var time = new FixedTime(Start);
        var fake = NewFake(time);
        var session = await fake.CreateSessionAsync(Request(), CancellationToken.None);
        var (body, headers) = fake.BuildCallback(session.GatewayRef, GatewayOutcome.Paid, eventId: "evt-1");

        var check = fake.VerifyCallback(body, headers, time.Now);

        Assert.True(check.Verified);
        Assert.Equal(("evt-1", session.GatewayRef, GatewayOutcome.Paid, 3150m), (check.Event!.EventId, check.Event.GatewayRef, check.Event.Outcome, check.Event.Amount));
    }

    [Fact]
    [Trait("Spec", "PAY-4")]
    public void ACallbackWithNoSignature_IsRejected()
    {
        var time = new FixedTime(Start);
        var fake = NewFake(time);

        var check = fake.VerifyCallback(Encoding.UTF8.GetBytes("{}"), new Dictionary<string, string>(), time.Now);

        Assert.False(check.Verified);
        Assert.Equal(CallbackRejection.MissingSignature, check.Rejection);
    }

    [Theory]
    [Trait("Spec", "PAY-4")]
    [InlineData("garbage")]
    [InlineData("t=1,v1=zz")]
    [InlineData("t=abc,v1=0000000000000000000000000000000000000000000000000000000000000000")]
    [InlineData("v1=0000000000000000000000000000000000000000000000000000000000000000")]
    [InlineData("t=1")]
    [InlineData("t=-5,v1=0000000000000000000000000000000000000000000000000000000000000000")]
    public void AMalformedSignatureHeader_IsRejected_NeverThrows(string header)
    {
        var time = new FixedTime(Start);
        var fake = NewFake(time);

        var check = fake.VerifyCallback(Encoding.UTF8.GetBytes("{}"), new Dictionary<string, string> { [FakeGateway.SignatureHeader] = header }, time.Now);

        Assert.False(check.Verified);
        Assert.Equal(CallbackRejection.BadSignature, check.Rejection);
    }

    [Fact]
    [Trait("Spec", "PAY-4")]
    public async Task ABodyChangedAfterSigning_IsRejected()
    {
        var time = new FixedTime(Start);
        var fake = NewFake(time);
        var session = await fake.CreateSessionAsync(Request(), CancellationToken.None);
        var (body, headers) = fake.BuildCallback(session.GatewayRef, GatewayOutcome.Paid, amount: 1m);
        var tampered = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(body).Replace("\"amount\":1", "\"amount\":3150", StringComparison.Ordinal));

        var check = fake.VerifyCallback(tampered, headers, time.Now);

        Assert.False(check.Verified);
        Assert.Equal(CallbackRejection.BadSignature, check.Rejection);
    }

    [Fact]
    [Trait("Spec", "PAY-4")]
    public async Task ACallbackSignedWithAnotherSecret_IsRejected()
    {
        var time = new FixedTime(Start);
        var attacker = NewFake(time, new OrdersOptions { GatewayProvider = "fake", FakeGatewayWebhookSecret = "attacker-secret-attacker-secret-0000" });
        var real = NewFake(time);
        var session = await attacker.CreateSessionAsync(Request(), CancellationToken.None);
        var (body, headers) = attacker.BuildCallback(session.GatewayRef, GatewayOutcome.Paid);

        var check = real.VerifyCallback(body, headers, time.Now);

        Assert.False(check.Verified);
    }

    [Theory]
    [Trait("Spec", "PAY-12")]
    [InlineData(-6)]
    [InlineData(-600)]
    [InlineData(6)]
    public async Task AValidSignatureThatIsTooOld_OrFromTheFuture_IsRefusedAsAReplay(int minutesFromNow)
    {
        var time = new FixedTime(Start);
        var fake = NewFake(time);
        var session = await fake.CreateSessionAsync(Request(), CancellationToken.None);
        var (body, headers) = fake.BuildCallback(session.GatewayRef, GatewayOutcome.Paid, at: Start.AddMinutes(minutesFromNow));

        var check = fake.VerifyCallback(body, headers, Start);

        Assert.False(check.Verified);
        Assert.Equal(CallbackRejection.StaleTimestamp, check.Rejection);
    }

    [Fact]
    [Trait("Spec", "PAY-12")]
    public async Task ASignedCallbackJustInsideTheWindow_IsAccepted()
    {
        var time = new FixedTime(Start);
        var fake = NewFake(time);
        var session = await fake.CreateSessionAsync(Request(), CancellationToken.None);
        var (body, headers) = fake.BuildCallback(session.GatewayRef, GatewayOutcome.Paid, at: Start.AddMinutes(-4));

        Assert.True(fake.VerifyCallback(body, headers, Start).Verified);
    }

    [Fact]
    [Trait("Spec", "SEC-10")]
    public void ASignedBodyThatIsNotWhatWeExpect_IsRejectedAsMalformed_NeverThrows()
    {
        var time = new FixedTime(Start);
        var fake = NewFake(time);
        foreach (var json in new[] { "not json", "{}", "[]", "{\"outcome\":\"maybe\",\"eventId\":\"e\",\"gatewayRef\":\"g\",\"amount\":1,\"occurredAt\":\"2026-11-09T10:00:00Z\"}", "{\"outcome\":\"paid\",\"eventId\":\"e\",\"gatewayRef\":\"g\",\"amount\":\"x\",\"occurredAt\":\"2026-11-09T10:00:00Z\"}" })
        {
            var body = Encoding.UTF8.GetBytes(json);
            var timestamp = time.Now.ToUnixTimeSeconds();
            using var hmac = new System.Security.Cryptography.HMACSHA256(Encoding.UTF8.GetBytes("test-only-fake-gateway-secret-0123456789"));
            var mac = Convert.ToHexString(hmac.ComputeHash([.. Encoding.UTF8.GetBytes(timestamp + "."), .. body])).ToLowerInvariant();

            var check = fake.VerifyCallback(body, new Dictionary<string, string> { [FakeGateway.SignatureHeader] = $"t={timestamp},v1={mac}" }, time.Now);

            Assert.False(check.Verified);
            Assert.Equal(CallbackRejection.Malformed, check.Rejection);
        }
    }

    [Fact]
    [Trait("Spec", "PAY-6")]
    public async Task TheStatus_ReflectsWhatTheGatewayDid_AndUnknownSessionsAreUnknown()
    {
        var fake = NewFake(new FixedTime(Start));
        var session = await fake.CreateSessionAsync(Request(), CancellationToken.None);

        Assert.Equal(GatewayStatusKind.Pending, (await fake.GetStatusAsync(session.GatewayRef, CancellationToken.None)).Kind);
        fake.SetOutcome(session.GatewayRef, GatewayOutcome.Paid);
        var paid = await fake.GetStatusAsync(session.GatewayRef, CancellationToken.None);
        Assert.Equal((GatewayStatusKind.Paid, 3150m), (paid.Kind, paid.Amount));
        Assert.Equal(GatewayStatusKind.Unknown, (await fake.GetStatusAsync("nope", CancellationToken.None)).Kind);
    }

    [Fact]
    [Trait("Spec", "PAY-16")]
    public async Task ARefundSentTwiceWithOneKey_RefundsOnce()
    {
        var fake = NewFake(new FixedTime(Start));
        var request = new RefundRequest("fk_x", 3150m, "refund-1");

        var first = await fake.RefundAsync(request, CancellationToken.None);
        var second = await fake.RefundAsync(request, CancellationToken.None);
        await fake.RefundAsync(request with { IdempotencyKey = "refund-2" }, CancellationToken.None);

        Assert.True(first.Succeeded);
        Assert.Equal(first, second);
        Assert.Equal(2, fake.RefundCount);
    }

    [Fact]
    public async Task WhenDown_EveryNetworkCallFails_ButVerifyingACallbackStillWorks()
    {
        var time = new FixedTime(Start);
        var fake = NewFake(time);
        var session = await fake.CreateSessionAsync(Request(), CancellationToken.None);
        var (body, headers) = fake.BuildCallback(session.GatewayRef, GatewayOutcome.Paid);
        fake.Mode = FakeGatewayMode.Down;

        await Assert.ThrowsAsync<GatewayUnavailableException>(() => fake.CreateSessionAsync(Request("x"), CancellationToken.None));
        await Assert.ThrowsAsync<GatewayUnavailableException>(() => fake.GetStatusAsync(session.GatewayRef, CancellationToken.None));
        await Assert.ThrowsAsync<GatewayUnavailableException>(() => fake.RefundAsync(new RefundRequest("x", 1m, "k"), CancellationToken.None));
        Assert.True(fake.VerifyCallback(body, headers, time.Now).Verified);
    }

    // ---- the options guard -----------------------------------------------------------------------------------------------------------

    [Theory]
    [Trait("Spec", "PAY-15")]
    [InlineData("Development", true)]
    [InlineData("Testing", true)]
    [InlineData("Staging", false)]
    [InlineData("Production", false)]
    public void TheFakeGateway_IsAllowedOnlyInDevelopmentAndTesting(string environment, bool allowed)
    {
        var validator = new OrdersGatewayOptionsValidator(new TestEnvironment(environment));

        var result = validator.Validate(null, new OrdersOptions { GatewayProvider = "fake" });

        Assert.Equal(allowed, result.Succeeded);
    }

    [Theory]
    [Trait("Spec", "PAY-15")]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void WithNoGatewayConfigured_AnyEnvironmentIsFine_AndSellsCashOnDeliveryOnly(string environment)
    {
        var validator = new OrdersGatewayOptionsValidator(new TestEnvironment(environment));
        var options = new OrdersOptions();

        Assert.True(validator.Validate(null, options).Succeeded);
        Assert.True(OrdersOptions.IsValid(options));
    }

    [Fact]
    [Trait("Spec", "PAY-15")]
    public void OnlineMethods_CannotBeSwitchedOn_WithoutAGateway()
    {
        Assert.False(OrdersOptions.IsValid(new OrdersOptions { EnabledOnlineMethods = ["CARD"] }));
        Assert.True(OrdersOptions.IsValid(new OrdersOptions { EnabledOnlineMethods = ["CARD"], GatewayProvider = "fake" }));
        Assert.False(OrdersOptions.IsValid(new OrdersOptions { GatewayProvider = "stripe" }));
    }

    [Fact]
    [Trait("Spec", "PAY-14")]
    public void TheMethodsOffered_AreCashOnDeliveryOnly_WhenTheGatewayIsNotAvailable()
    {
        var options = new OrdersOptions { EnabledOnlineMethods = ["EASY", "JAZZ", "CARD"], GatewayProvider = "fake" };

        Assert.Equal(["COD", "EASY", "JAZZ", "CARD"], PaymentMethodCatalog.Available(options, true).Select(m => m.Code));
        Assert.Equal(["COD"], PaymentMethodCatalog.Available(options, false).Select(m => m.Code));
        Assert.Equal(["COD"], PaymentMethodCatalog.Available(new OrdersOptions(), true).Select(m => m.Code));
    }

    private sealed class TestEnvironment(string name) : Microsoft.Extensions.Hosting.IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;

        public string ApplicationName { get; set; } = "Test";

        public string ContentRootPath { get; set; } = "/";

        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
