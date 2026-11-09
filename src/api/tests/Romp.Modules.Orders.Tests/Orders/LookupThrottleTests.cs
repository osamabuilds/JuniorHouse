using Microsoft.EntityFrameworkCore;
using Romp.Modules.Orders.Domain.Orders;
using Romp.Modules.Orders.Tests.Support;

namespace Romp.Modules.Orders.Tests.Orders;

public sealed class LookupThrottleTests(OrdersPostgres postgres) : IClassFixture<OrdersPostgres>
{
    private static readonly string Hash = new('a', LookupAttempt.AddressHashLength);

    [Theory]
    [Trait("Spec", "OLK-4")]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(3, true)]
    [InlineData(10, true)]
    public void TheCaptcha_IsAskedForFromTheThirdFailure(int failures, bool captcha) =>
        Assert.Equal(captcha, LookupThrottle.Evaluate(failures, failures).CaptchaRequired);

    [Theory]
    [Trait("Spec", "OLK-3")]
    [InlineData(0, false)]
    [InlineData(19, false)]
    [InlineData(20, true)]
    [InlineData(500, true)]
    public void AnAddress_IsRateLimitedAtTwentyAttemptsAnHour(int attempts, bool limited) =>
        Assert.Equal(limited, LookupThrottle.Evaluate(0, attempts).RateLimited);

    [Fact]
    [Trait("Spec", "OLK-3")]
    public void AnAttempt_RejectsAnAddressThatIsNotAHash()
    {
        Assert.Throws<ArgumentException>(() => LookupAttempt.Record("203.0.113.9", failed: true, DateTimeOffset.UtcNow));
        Assert.Throws<ArgumentException>(() => LookupAttempt.Record(" ", failed: true, DateTimeOffset.UtcNow));
    }

    [Fact]
    [Trait("Spec", "OLK-3")]
    [Trait("Spec", "OLK-4")]
    public async Task Attempts_SaveAndReadBackByAddressAndTime()
    {
        var now = DateTimeOffset.UtcNow;
        await using (var db = postgres.NewContext())
        {
            db.LookupAttempts.Add(LookupAttempt.Record(Hash, failed: true, now.AddMinutes(-5)));
            db.LookupAttempts.Add(LookupAttempt.Record(Hash, failed: false, now));
            await db.SaveChangesAsync();
        }

        await using var read = postgres.NewContext();
        var rows = await read.LookupAttempts.Where(a => a.AddressHash == Hash).OrderByDescending(a => a.AttemptedAt).ToListAsync();
        Assert.Equal([false, true], rows.Select(a => a.Failed));
    }
}
