using System.Text.Json;
using Microsoft.Extensions.Options;
using Romp.Modules.Shipping.Application.Abstractions;
using Romp.Modules.Shipping.Application.CourierPort;
using Romp.Modules.Shipping.Domain;
using Romp.Modules.Shipping.Infrastructure.CourierPort;

namespace Romp.Modules.Shipping.Tests.CourierPort;

/// <summary>S14 S3: the courier port and its pretend courier (CPT-1 to CPT-8).</summary>
public sealed class FakeCourierTests
{
    private const string Secret = "a-test-secret-of-sixteen-plus";

    private static readonly BookingRequest Request = new("RMP-1", "Sara Khan", "03001234567", "House 4", null, "Lahore", "Punjab", "54000", null, 4700m, 600);

    private static FakeCourierGateway Gateway(FakeCourierState? state = null, short id = Couriers.Primary) => new(id, Secret, true, state ?? new FakeCourierState());

    [Fact]
    [Trait("Spec", "CPT-1")]
    public async Task ABooking_AnswersAWaybill_ThatIsDifferentEachTime()
    {
        var courier = Gateway();

        var a = await courier.BookAsync(Request, CancellationToken.None);
        var b = await courier.BookAsync(Request, CancellationToken.None);

        Assert.Equal(BookingOutcome.Booked, a.Outcome);
        Assert.StartsWith("PRM-", a.Waybill);
        Assert.NotEqual(a.Waybill, b.Waybill);
    }

    [Fact]
    [Trait("Spec", "CPT-6")]
    public async Task AnOutage_MakesEveryBookingUnreachable_UntilItIsSwitchedOff()
    {
        var state = new FakeCourierState();
        var courier = Gateway(state);
        state.Outage[Couriers.Primary] = true;

        var down = await courier.BookAsync(Request, CancellationToken.None);
        state.Outage[Couriers.Primary] = false;
        var up = await courier.BookAsync(Request, CancellationToken.None);

        Assert.Equal(BookingOutcome.Unreachable, down.Outcome);
        Assert.Equal(BookingOutcome.Booked, up.Outcome);
    }

    [Fact]
    [Trait("Spec", "CPT-3")]
    public async Task ARefusedAddress_IsRejectedWithAReason_NotAnOutage()
    {
        var result = await Gateway().BookAsync(Request with { AddressLine1 = "REJECT road" }, CancellationToken.None);

        Assert.Equal(BookingOutcome.Rejected, result.Outcome);
        Assert.False(string.IsNullOrWhiteSpace(result.Reason));
    }

    [Fact]
    [Trait("Spec", "CPT-4")]
    public async Task StatusOfAWaybill_ComesFromTheScript_OrIsNullWhenUnknown()
    {
        var state = new FakeCourierState();
        var courier = Gateway(state);
        state.Script["PRM-1"] = new CourierStatus("IN_TRANSIT", "e1", DateTimeOffset.UtcNow);

        Assert.Equal("IN_TRANSIT", (await courier.StatusAsync("PRM-1", CancellationToken.None))!.StatusCode);
        Assert.Null(await courier.StatusAsync("PRM-404", CancellationToken.None));
    }

    private static string Body(string waybill = "PRM-1", string status = "PICKED_UP") =>
        JsonSerializer.Serialize(new { waybill, status, eventId = "evt-1", at = DateTimeOffset.Parse("2026-11-09T10:00:00Z", System.Globalization.CultureInfo.InvariantCulture) });

    [Fact]
    [Trait("Spec", "CPT-5")]
    public void ASignedPush_IsParsed_WithWaybillStatusEventIdAndTime()
    {
        var body = Body("prm-1");

        var push = Gateway().ParsePush(body, FakeCourierGateway.Sign(Secret, body));

        Assert.NotNull(push);
        Assert.Equal("PRM-1", push.Waybill);
        Assert.Equal(("PICKED_UP", "evt-1"), (push.Status.StatusCode, push.Status.CourierEventId));
    }

    [Theory]
    [Trait("Spec", "CPT-5")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("deadbeef")]
    public void APushWithAMissingOrWrongSignature_IsInvalid(string? signature) => Assert.Null(Gateway().ParsePush(Body(), signature));

    [Fact]
    [Trait("Spec", "CPT-5")]
    public void ASignatureMadeWithAnotherSecret_OrOverAnotherBody_IsInvalid()
    {
        var body = Body();

        Assert.Null(Gateway().ParsePush(body, FakeCourierGateway.Sign("another-secret-of-sixteen-plus", body)));
        Assert.Null(Gateway().ParsePush(body.Replace("PICKED_UP", "DELIVERED", StringComparison.Ordinal), FakeCourierGateway.Sign(Secret, body)));
    }

    [Theory]
    [Trait("Spec", "CPT-5")]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("{\"waybill\":\"A\",\"status\":\"X\",\"eventId\":\"\",\"at\":\"2026-11-09T10:00:00Z\"}")]
    public void ASignedBodyThatIsNotAStatus_IsInvalid(string body) => Assert.Null(Gateway().ParsePush(body, FakeCourierGateway.Sign(Secret, body)));

    [Fact]
    [Trait("Spec", "CPT-7")]
    public void TheRegistry_KnowsOnlyEnabledCouriers_PrimaryFirst()
    {
        var options = new ShippingOptions();
        options.Couriers["SECONDARY"] = new CourierSettings { WebhookSecret = Secret };
        options.Couriers["PRIMARY"] = new CourierSettings { WebhookSecret = Secret };
        options.Couriers["OTHER"] = new CourierSettings { WebhookSecret = Secret };
        var registry = new CourierRegistry(Options.Create(options), new FakeCourierState());
        options.Couriers["SECONDARY"].Enabled = false;
        var onlyPrimary = new CourierRegistry(Options.Create(options), new FakeCourierState());

        Assert.Equal([Couriers.Primary, Couriers.Secondary], registry.InOrder.Select(g => g.CourierId));
        Assert.Equal([Couriers.Primary], onlyPrimary.InOrder.Select(g => g.CourierId));
        Assert.Null(registry.FindByCode("OTHER"));
        Assert.Null(registry.FindByCode(null));
        Assert.Null(onlyPrimary.FindByCode("SECONDARY"));
    }

    [Fact]
    [Trait("Spec", "CPT-8")]
    public void TheCourierEventsAndAnswers_CarryNoMoreThanTheyNeed()
    {
        var push = Gateway().ParsePush(Body(), FakeCourierGateway.Sign(Secret, Body()))!;

        var json = JsonSerializer.Serialize(push);

        Assert.DoesNotContain("Sara", json);
        Assert.DoesNotContain("0300", json);
    }
}
