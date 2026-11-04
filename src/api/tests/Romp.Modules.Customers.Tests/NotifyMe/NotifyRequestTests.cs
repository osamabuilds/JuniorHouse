using Romp.Modules.Customers.Application;
using Romp.Modules.Customers.Domain.Abstractions;
using Romp.Modules.Customers.Domain.NotifyMe;

namespace Romp.Modules.Customers.Tests.NotifyMe;

/// <summary>customers/notify-me N1 (NTM-2, NTM-3, NTM-9): the request lifecycle, the restock rule, email checks and the options, all pure.</summary>
public sealed class NotifyRequestTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 10, 0, 0, TimeSpan.Zero);

    [Theory]
    [Trait("Spec", "NTM-2")]
    [InlineData("  Sara@Example.COM ", "sara@example.com")]
    [InlineData("a.b+tag@mail.example.pk", "a.b+tag@mail.example.pk")]
    public void Normalize_TrimsAndLowersTheAddress(string raw, string expected) =>
        Assert.Equal(expected, NotifyEmail.Normalize(raw));

    [Theory]
    [Trait("Spec", "NTM-6")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("no-at-sign")]
    [InlineData("a@b@example.com")]
    [InlineData("sara@nodot")]
    [InlineData("sara@exa..mple.com")]
    [InlineData("sara@example.com\r\nBcc: victim@example.com")]
    public void Normalize_RefusesAnythingThatIsNotAnAddress(string? raw) =>
        Assert.Null(NotifyEmail.Normalize(raw));

    [Fact]
    [Trait("Spec", "NTM-2")]
    public void Open_StartsAnOpenRequestForTheVariant()
    {
        var request = NotifyRequest.Open(42, "sara@example.com", Now);

        Assert.Equal(NotifyStatus.Open, request.Status);
        Assert.Equal(42, request.VariantId);
        Assert.Equal("sara@example.com", request.Email);
        Assert.Null(request.NotifiedAt);
        Assert.Null(request.SentAt);
    }

    [Fact]
    [Trait("Spec", "NTM-2")]
    public void Open_RefusesAnAddressThatIsNotNormalised() =>
        Assert.Throws<ArgumentException>(() => NotifyRequest.Open(42, "Sara@Example.com", Now));

    [Theory]
    [Trait("Spec", "NTM-2")]
    [InlineData(0)]
    [InlineData(-1)]
    public void Open_RefusesAVariantIdThatIsNotPositive(long variantId) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => NotifyRequest.Open(variantId, "sara@example.com", Now));

    [Fact]
    [Trait("Spec", "NTM-4")]
    public void MarkNotified_ClosesTheRequestExactlyOnce()
    {
        var request = NotifyRequest.Open(42, "sara@example.com", Now);

        Assert.True(request.MarkNotified(Now.AddHours(1)));
        Assert.False(request.MarkNotified(Now.AddHours(2)));
        Assert.Equal(NotifyStatus.Notified, request.Status);
        Assert.Equal(Now.AddHours(1), request.NotifiedAt);
    }

    [Fact]
    [Trait("Spec", "NTM-4")]
    public void MarkNotified_DoesNothingToAnExpiredRequest()
    {
        var request = NotifyRequest.Open(42, "sara@example.com", Now);
        request.Expire(Now.AddDays(90));

        Assert.False(request.MarkNotified(Now.AddDays(91)));
        Assert.Equal(NotifyStatus.Expired, request.Status);
    }

    [Fact]
    public void Expire_OnlyAffectsAnOpenRequest()
    {
        var request = NotifyRequest.Open(42, "sara@example.com", Now);
        request.MarkNotified(Now);

        Assert.False(request.Expire(Now.AddDays(90)));
        Assert.Equal(NotifyStatus.Notified, request.Status);
    }

    [Fact]
    [Trait("Spec", "NTM-5")]
    public void MarkSent_RecordsTheSendOnceAndOnlyForANotifiedRequest()
    {
        var request = NotifyRequest.Open(42, "sara@example.com", Now);

        Assert.False(request.MarkSent(Now));
        request.MarkNotified(Now);
        Assert.True(request.MarkSent(Now.AddMinutes(1)));
        Assert.False(request.MarkSent(Now.AddMinutes(2)));
        Assert.Equal(Now.AddMinutes(1), request.SentAt);
    }

    [Fact]
    [Trait("Spec", "NTM-4")]
    public void QueueRestockNotice_RaisesOneEventNamingTheRecordOnly()
    {
        var request = NotifyRequest.Open(42, "sara@example.com", Now);
        Assert.Throws<InvalidOperationException>(request.QueueRestockNotice);

        typeof(Romp.BuildingBlocks.Domain.Entity<long>).GetProperty("Id", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)!.SetValue(request, 7L);
        request.QueueRestockNotice();

        var raised = Assert.IsType<CustomerEmailRequestedEvent>(Assert.Single(request.DomainEvents));
        Assert.Equal(CustomerEmailKind.RestockNotice, raised.Kind);
        Assert.Equal(7, raised.RelatedId);
    }

    [Theory]
    [Trait("Spec", "NTM-4")]
    [InlineData(1, true)]
    [InlineData(25, true)]
    [InlineData(0, false)]
    [InlineData(-3, false)]
    public void RestockRule_FiresOnlyWhenStockIsAvailable(int available, bool expected) =>
        Assert.Equal(expected, RestockRule.IsRestock(available));

    [Fact]
    [Trait("Spec", "NTM-3")]
    public void RestockRule_HasRoomBelowTheCap()
    {
        Assert.True(RestockRule.HasRoom(4999, 5000));
        Assert.False(RestockRule.HasRoom(5000, 5000));
    }

    [Fact]
    public void Options_HaveTheDefaultsAndAreValidated()
    {
        var options = new CustomersOptions();
        Assert.Equal(5000, options.MaxNotifyPerVariant);
        Assert.Equal(90, options.NotifyOpenDays);
        Assert.Equal(30, options.NotifyClosedDays);
        Assert.True(CustomersOptions.IsValid(options));

        Assert.False(CustomersOptions.IsValid(new CustomersOptions { MaxNotifyPerVariant = 0 }));
        Assert.False(CustomersOptions.IsValid(new CustomersOptions { NotifyOpenDays = 0 }));
        Assert.False(CustomersOptions.IsValid(new CustomersOptions { NotifyClosedDays = 0 }));
    }
}
