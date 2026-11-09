using Romp.BuildingBlocks.Domain;
using Romp.Modules.Notifications.Domain.Delivery;

namespace Romp.Modules.Notifications.Tests.Delivery;

public sealed class RetryPolicyTests
{
    [Fact]
    [Trait("Spec", "DLV-7")]
    public void Constructor_WithFewerThanFiveAttempts_IsRefused()
    {
        var delays = Enumerable.Repeat(TimeSpan.FromMinutes(1), 4);

        Assert.Throws<DomainException>(() => new RetryPolicy(delays, 0.2));
    }

    [Fact]
    [Trait("Spec", "DLV-7")]
    public void Default_HasSixAttemptsAndTheApprovedSchedule()
    {
        var policy = RetryPolicy.Default;

        Assert.Equal(6, policy.MaxAttempts);
        Assert.Equal(TimeSpan.FromMinutes(1), policy.DelayAfter(1, 0));
        Assert.Equal(TimeSpan.FromMinutes(5), policy.DelayAfter(2, 0));
        Assert.Equal(TimeSpan.FromHours(6), policy.DelayAfter(5, 0));
    }

    [Theory]
    [InlineData(-1, 48)]
    [InlineData(0, 60)]
    [InlineData(1, 72)]
    [InlineData(5, 72)]
    [Trait("Spec", "DLV-7")]
    public void DelayAfter_StaysWithinTheJitterBand(double sample, int expectedSeconds)
    {
        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), RetryPolicy.Default.DelayAfter(1, sample));
    }

    [Fact]
    [Trait("Spec", "DLV-7")]
    public void DelayAfter_TheLastAttempt_HasNoRetry()
    {
        Assert.Throws<DomainException>(() => RetryPolicy.Default.DelayAfter(6, 0));
    }
}

public sealed class ChannelDeliveryTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 3, 10, 0, 0, TimeSpan.Zero);

    private static ChannelDelivery Email() => new(NotificationChannel.Email, Now);

    [Fact]
    [Trait("Spec", "DLV-2")]
    public void RecordFailure_Temporary_RequeuesWithBackoffUntilItSucceeds()
    {
        var delivery = Email();

        delivery.RecordFailure(ErrorCategory.Temporary, Now, RetryPolicy.Default, 0);
        delivery.RecordFailure(ErrorCategory.Temporary, Now, RetryPolicy.Default, 0);

        Assert.Equal(DeliveryStatus.Queued, delivery.Status);
        Assert.Equal(2, delivery.AttemptCount);
        Assert.Equal(Now + TimeSpan.FromMinutes(5), delivery.NextAttemptAt);

        delivery.MarkSent("m-1");
        Assert.Equal(DeliveryStatus.Sent, delivery.Status);
        Assert.Equal(3, delivery.AttemptCount);
    }

    [Fact]
    [Trait("Spec", "DLV-3")]
    public void RecordFailure_WhenAttemptsRunOut_IsDead()
    {
        var delivery = Email();

        for (var i = 0; i < RetryPolicy.Default.MaxAttempts; i++)
        {
            delivery.RecordFailure(ErrorCategory.Temporary, Now, RetryPolicy.Default, 0);
        }

        Assert.Equal(DeliveryStatus.Dead, delivery.Status);
        Assert.Null(delivery.NextAttemptAt);
        Assert.True(delivery.IsFinal);
    }

    [Fact]
    [Trait("Spec", "DLV-8")]
    public void RecordFailure_Permanent_FailsAtOnceWithoutRetry()
    {
        var delivery = Email();

        delivery.RecordFailure(ErrorCategory.Permanent, Now, RetryPolicy.Default, 0);

        Assert.Equal(DeliveryStatus.Failed, delivery.Status);
        Assert.Equal(1, delivery.AttemptCount);
        Assert.Equal(ErrorCategory.Permanent, delivery.LastError);
    }

    [Fact]
    [Trait("Spec", "CHN-4")]
    public void RecordFailure_CircuitOpen_WaitsWithoutUsingAnAttempt()
    {
        var delivery = Email();

        delivery.RecordFailure(ErrorCategory.CircuitOpen, Now, RetryPolicy.Default, 0);

        Assert.Equal(DeliveryStatus.Queued, delivery.Status);
        Assert.Equal(0, delivery.AttemptCount);
        Assert.Equal(Now + TimeSpan.FromMinutes(1), delivery.NextAttemptAt);
    }

    [Fact]
    [Trait("Spec", "DLV-9")]
    public void TryClaim_WhileLeased_IsRefusedUntilTheLeaseEnds()
    {
        var delivery = Email();

        Assert.True(delivery.TryClaim(Now, TimeSpan.FromMinutes(2)));
        Assert.False(delivery.TryClaim(Now.AddMinutes(1), TimeSpan.FromMinutes(2)));
        Assert.True(delivery.TryClaim(Now.AddMinutes(3), TimeSpan.FromMinutes(2)));
    }

    [Fact]
    [Trait("Spec", "DLV-2")]
    public void TryClaim_BeforeTheNextAttemptIsDue_IsRefused()
    {
        var delivery = Email();
        delivery.RecordFailure(ErrorCategory.Temporary, Now, RetryPolicy.Default, 0);

        Assert.False(delivery.TryClaim(Now.AddSeconds(30), TimeSpan.FromMinutes(2)));
        Assert.True(delivery.TryClaim(Now.AddMinutes(1), TimeSpan.FromMinutes(2)));
    }

    [Fact]
    [Trait("Spec", "DLV-10")]
    public void Expire_AQueuedDelivery_IsFinal()
    {
        var delivery = Email();

        delivery.Expire();

        Assert.Equal(DeliveryStatus.Expired, delivery.Status);
        Assert.False(delivery.IsDue(Now.AddDays(1)));
    }

    [Fact]
    [Trait("Spec", "CHN-2")]
    public void Skip_ForAnOptOut_IsFinal_AndOtherReasonsAreRefused()
    {
        var delivery = new ChannelDelivery(NotificationChannel.WhatsApp, Now);

        Assert.Throws<DomainException>(() => delivery.Skip(ErrorCategory.Temporary));
        delivery.Skip(ErrorCategory.OptedOut);

        Assert.Equal(DeliveryStatus.Skipped, delivery.Status);
        Assert.Equal(ErrorCategory.OptedOut, delivery.LastError);
    }

    [Fact]
    [Trait("Spec", "DLV-4")]
    public void AFinalDelivery_CannotBeSentAgain()
    {
        var delivery = Email();
        delivery.MarkSent("m-1");

        Assert.Throws<DomainException>(() => delivery.MarkSent("m-2"));
        Assert.Throws<DomainException>(delivery.Expire);
        Assert.Throws<DomainException>(() => delivery.RecordFailure(ErrorCategory.Temporary, Now, RetryPolicy.Default, 0));
    }

    [Fact]
    [Trait("Spec", "CHN-14")]
    public void MarkDelivered_NeedsSent_AndIsIdempotent()
    {
        var delivery = new ChannelDelivery(NotificationChannel.WhatsApp, Now);
        Assert.Throws<DomainException>(delivery.MarkDelivered);

        delivery.MarkSent("w-1");
        delivery.MarkDelivered();
        delivery.MarkDelivered();

        Assert.Equal(DeliveryStatus.Delivered, delivery.Status);
    }
}

public sealed class ChannelPlanTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 3, 10, 0, 0, TimeSpan.Zero);

    private static Notification NewNotification() =>
        new(NotificationKind.OrderConfirmation, "ORDR", 42, Guid.NewGuid(), Now, TimeSpan.FromDays(7), 1);

    [Fact]
    [Trait("Spec", "CHN-1")]
    public void OptedIn_PlansEmailAndWhatsApp()
    {
        var notification = NewNotification();

        notification.Plan(ChannelPlan.For(whatsAppOptIn: true, numberSuppressed: false), Now);

        Assert.Equal(
            [NotificationChannel.Email, NotificationChannel.WhatsApp],
            notification.Deliveries.Select(d => d.Channel).ToArray());
        Assert.All(notification.Deliveries, d => Assert.Equal(DeliveryStatus.Queued, d.Status));
    }

    [Fact]
    [Trait("Spec", "CHN-2")]
    public void NotOptedIn_NeverSendsWhatsApp_ButRecordsWhy()
    {
        var notification = NewNotification();

        notification.Plan(ChannelPlan.For(whatsAppOptIn: false, numberSuppressed: false), Now);

        var whatsApp = notification.Deliveries.Single(d => d.Channel == NotificationChannel.WhatsApp);
        Assert.Equal(DeliveryStatus.Skipped, whatsApp.Status);
        Assert.False(whatsApp.IsDue(Now));
        Assert.Equal(DeliveryStatus.Queued, notification.Deliveries.Single(d => d.Channel == NotificationChannel.Email).Status);
    }

    [Fact]
    [Trait("Spec", "CHN-6")]
    public void ASuppressedNumber_IsSkippedEvenWhenOptedIn()
    {
        var notification = NewNotification();

        notification.Plan(ChannelPlan.For(whatsAppOptIn: true, numberSuppressed: true), Now);

        Assert.Equal(DeliveryStatus.Skipped, notification.Deliveries.Single(d => d.Channel == NotificationChannel.WhatsApp).Status);
    }

    [Fact]
    [Trait("Spec", "CHN-3")]
    public void AWhatsAppFailure_LeavesTheEmailStanding()
    {
        var notification = NewNotification();
        notification.Plan(ChannelPlan.For(true, false), Now);
        var email = notification.Deliveries.Single(d => d.Channel == NotificationChannel.Email);
        var whatsApp = notification.Deliveries.Single(d => d.Channel == NotificationChannel.WhatsApp);

        whatsApp.RecordFailure(ErrorCategory.Permanent, Now, RetryPolicy.Default, 0);

        Assert.Equal(DeliveryStatus.Failed, whatsApp.Status);
        Assert.Equal(DeliveryStatus.Queued, email.Status);
    }

    [Fact]
    [Trait("Spec", "DLV-4")]
    public void AddDelivery_TwiceForOneChannel_IsRefused()
    {
        var notification = NewNotification();
        notification.AddDelivery(NotificationChannel.Email, Now);

        Assert.Throws<DomainException>(() => notification.AddDelivery(NotificationChannel.Email, Now));
    }

    [Fact]
    [Trait("Spec", "DLV-10")]
    public void IsExpired_AfterTheKindsLifetime()
    {
        var notification = NewNotification();

        Assert.False(notification.IsExpired(Now.AddDays(6)));
        Assert.True(notification.IsExpired(Now.AddDays(7)));
    }
}
