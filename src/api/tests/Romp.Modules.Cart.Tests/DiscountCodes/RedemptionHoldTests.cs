using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Romp.Modules.Cart.Application;
using Romp.Modules.Cart.Application.Carts;
using Romp.Modules.Cart.Application.DiscountCodes;
using Romp.Modules.Cart.Contracts;
using Romp.Modules.Cart.Domain.Carts;
using Romp.Modules.Cart.Domain.DiscountCodes;
using Romp.Modules.Cart.Infrastructure.Persistence;
using Romp.Modules.Cart.Tests.Support;

namespace Romp.Modules.Cart.Tests.DiscountCodes;

public sealed class RedemptionHoldTests(CartPostgres postgres) : IClassFixture<CartPostgres>
{
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);
    private static int _seq = 70_000;

    private readonly FakeCatalog _catalog = new();
    private readonly FakeStock _stock = new();
    private readonly FakeImages _images = new();

    private sealed class MovableTime(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Value { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Value;
    }

    private readonly MovableTime _time = new(Now);

    private async Task<CartOwner> BasketWithCodeAsync(string code, bool guest = false)
    {
        var id = Interlocked.Increment(ref _seq);
        _catalog.Add(id, 1000m);
        _stock.Available[9000 + id] = 50;
        var owner = guest ? CartOwner.ForGuest(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(BitConverter.GetBytes(id)))) : CartOwner.ForCustomer(id);
        await using var db = postgres.NewContext();
        var support = new CartCommandSupport(
            db, _catalog, _stock, new CartPricer(_catalog, _stock, _images), Options.Create(new CartOptions()), _time, new DiscountService(db), new CartNotices());
        await new AddLineCommandHandler(support).Handle(new AddLineCommand(owner, id, 2), CancellationToken.None);
        await db.SaveChangesAsync();
        await using var db2 = postgres.NewContext();
        await new ApplyDiscountCommandHandler(db2, new CartPricer(_catalog, _stock, _images), new DiscountService(db2), _time, Options.Create(new CartOptions()))
            .Handle(new ApplyDiscountCommand(owner, code), CancellationToken.None);
        await db2.SaveChangesAsync();
        return owner;
    }

    private async Task<string> NewCodeAsync(int? cap = null, int? perCustomer = null)
    {
        var code = $"H{Interlocked.Increment(ref _seq)}";
        await using var db = postgres.NewContext();
        db.DiscountCodes.Add(DiscountCode.Create(code, DiscountKind.Percentage, 10m, null, null, cap, perCustomer, false, [], Now.AddDays(-1)));
        await db.SaveChangesAsync();
        return code;
    }

    /// <summary>The hold in its own context and transaction, as the host runs it.</summary>
    private async Task<DiscountHold> HoldAsync(CartOwner owner, string key, string? mobileHash = null)
    {
        await using var db = postgres.NewContext();
        var handler = new HoldRedemptionCommandHandler(db, new CartPricer(_catalog, _stock, _images), new DiscountService(db), _time);
        var command = new HoldRedemptionCommand(owner, key, mobileHash);
        return await new CartTransactionBehavior<HoldRedemptionCommand, DiscountHold>(db).Handle(command, _ => handler.Handle(command, CancellationToken.None), CancellationToken.None);
    }

    private async Task ReleaseAsync(string key)
    {
        await using var db = postgres.NewContext();
        var command = new ReleaseRedemptionCommand(key);
        await new CartTransactionBehavior<ReleaseRedemptionCommand, MediatR.Unit>(db).Handle(
            command, async _ => { await new ReleaseRedemptionCommandHandler(db).Handle(command, CancellationToken.None); return MediatR.Unit.Value; }, CancellationToken.None);
    }

    private async Task<int> CountAsync(string code, RedemptionStatus status)
    {
        await using var db = postgres.NewContext();
        return await db.Redemptions.CountAsync(r => r.Status == status && db.DiscountCodes.Any(c => c.Code == code && c.Id == r.DiscountCodeId));
    }

    [Fact]
    [Trait("Spec", "DSC-10")]
    public async Task TwoHoldsForTheLastUse_OnlyOneWins()
    {
        var code = await NewCodeAsync(cap: 1);
        var first = await BasketWithCodeAsync(code);
        var second = await BasketWithCodeAsync(code);

        var results = await Task.WhenAll(HoldAsync(first, $"k-{code}-1"), HoldAsync(second, $"k-{code}-2"));

        Assert.Equal(1, results.Count(r => r.Applied));
        Assert.Equal("That code is no longer available.", results.Single(r => !r.Applied).Refusal);
        Assert.Equal(1, await CountAsync(code, RedemptionStatus.Held));
    }

    [Fact]
    [Trait("Spec", "DSC-10")]
    public async Task TheSamePlacementKey_IsTheSameHold()
    {
        var code = await NewCodeAsync(cap: 5);
        var owner = await BasketWithCodeAsync(code);

        var first = await HoldAsync(owner, $"k-{code}");
        var again = await HoldAsync(owner, $"k-{code}");

        Assert.True(first.Applied);
        Assert.Equal(200m, first.Amount);
        Assert.Equal(first, again);
        Assert.Equal(1, await CountAsync(code, RedemptionStatus.Held));
    }

    [Fact]
    [Trait("Spec", "DSC-10")]
    public async Task AReleasedHold_GivesTheUseBack()
    {
        var code = await NewCodeAsync(cap: 1);
        var first = await BasketWithCodeAsync(code);
        var second = await BasketWithCodeAsync(code);
        await HoldAsync(first, $"k-{code}-1");
        Assert.False((await HoldAsync(second, $"k-{code}-2")).Applied);

        await ReleaseAsync($"k-{code}-1");

        Assert.True((await HoldAsync(second, $"k-{code}-3")).Applied);
    }

    [Fact]
    [Trait("Spec", "DSC-3")]
    public async Task ASignedInCustomer_CannotHoldTheirOneUseTwice()
    {
        var code = await NewCodeAsync(perCustomer: 1);
        var owner = await BasketWithCodeAsync(code);
        Assert.True((await HoldAsync(owner, $"k-{code}-1")).Applied);

        var second = await HoldAsync(owner, $"k-{code}-2");

        Assert.False(second.Applied);
        Assert.Equal("You have already used that code.", second.Refusal);
    }

    [Fact]
    [Trait("Spec", "DSC-3")]
    public async Task AGuest_IsLimitedByTheirMobileHash()
    {
        var code = await NewCodeAsync(perCustomer: 1);
        var one = await BasketWithCodeAsync(code, guest: true);
        var two = await BasketWithCodeAsync(code, guest: true);
        Assert.True((await HoldAsync(one, $"k-{code}-1", "abc123")).Applied);

        Assert.False((await HoldAsync(two, $"k-{code}-2", "abc123")).Applied);
        Assert.True((await HoldAsync(two, $"k-{code}-3", "other456")).Applied);
    }

    [Fact]
    [Trait("Spec", "DSC-10")]
    public async Task ABasketWithNoCode_HoldsNothing()
    {
        var owner = CartOwner.ForCustomer(Interlocked.Increment(ref _seq));

        var hold = await HoldAsync(owner, $"k-none-{_seq}");

        Assert.False(hold.Applied);
        Assert.Null(hold.Refusal);
    }

    [Fact]
    [Trait("Spec", "DSC-10")]
    public async Task TheSweeper_ReleasesHoldsOlderThanTheLimit_AndLeavesConfirmedOnesAlone()
    {
        var code = await NewCodeAsync();
        var old = await BasketWithCodeAsync(code);
        var confirmed = await BasketWithCodeAsync(code);
        await HoldAsync(old, $"k-{code}-old");
        await HoldAsync(confirmed, $"k-{code}-confirmed");
        await using (var db = postgres.NewContext())
        {
            (await db.Redemptions.SingleAsync(r => r.PlacementKey == $"k-{code}-confirmed")).Confirm("RMP-2026-00001-1");
            await db.SaveChangesAsync();
        }

        // The test context has no audit interceptor, so the write times are set by hand: two holds are 40 minutes old, one is 5.
        var fresh = await BasketWithCodeAsync(code);
        await HoldAsync(fresh, $"k-{code}-fresh");
        await using (var db = postgres.NewContext())
        {
            var stale = Now.AddMinutes(-40);
            var recent = Now.AddMinutes(-5);
            await db.Redemptions.Where(r => r.PlacementKey == $"k-{code}-old" || r.PlacementKey == $"k-{code}-confirmed").ExecuteUpdateAsync(s => s.SetProperty(r => r.InsrDte, stale));
            await db.Redemptions.Where(r => r.PlacementKey == $"k-{code}-fresh").ExecuteUpdateAsync(s => s.SetProperty(r => r.InsrDte, recent));
        }

        await using (var db = postgres.NewContext())
        {
            var released = await new SweepHeldRedemptionsCommandHandler(db, _time, Options.Create(new CartOptions())).Handle(new SweepHeldRedemptionsCommand(), CancellationToken.None);
            await db.SaveChangesAsync();
            Assert.True(released >= 1);
        }

        await using var check = postgres.NewContext();
        var states = await check.Redemptions.Where(r => r.PlacementKey.StartsWith($"k-{code}-")).ToDictionaryAsync(r => r.PlacementKey, r => r.Status);
        Assert.Equal(RedemptionStatus.Released, states[$"k-{code}-old"]);
        Assert.Equal(RedemptionStatus.Held, states[$"k-{code}-fresh"]);
        Assert.Equal(RedemptionStatus.Confirmed, states[$"k-{code}-confirmed"]);
    }
}
