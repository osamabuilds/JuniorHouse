using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Romp.Modules.Cart.Application;
using Romp.Modules.Cart.Application.Carts;
using Romp.Modules.Cart.Domain.Carts;
using Romp.Modules.Cart.Tests.Support;
using Romp.Modules.Customers.Contracts;

namespace Romp.Modules.Cart.Tests.Carts;

public sealed class MergeGuestCartTests(CartPostgres postgres) : IClassFixture<CartPostgres>
{
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Guest = TimeSpan.FromDays(30);

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static string NewGuestKey() => Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private async Task SaveAsync(ShoppingCart cart)
    {
        await using var db = postgres.NewContext();
        db.Carts.Add(cart);
        await db.SaveChangesAsync();
    }

    private async Task MergeAsync(long customerId, string guestKey, DateTimeOffset? at = null)
    {
        await using var db = postgres.NewContext();
        var catalog = new FakeCatalog();
        var stock = new FakeStock();
        var support = new CartCommandSupport(db, catalog, stock, new CartPricer(catalog, stock), Options.Create(new CartOptions()), new FixedClock(at ?? Now));
        await new MergeGuestCartCommandHandler(support, db).Handle(new MergeGuestCartCommand(customerId, guestKey), CancellationToken.None);
        await db.SaveChangesAsync();
    }

    private static ShoppingCart GuestWith(string key, params (long Variant, int Quantity)[] lines)
    {
        Assert.True(CartOwner.TryHashGuestKey(key, out var hash));
        var cart = ShoppingCart.Create(CartOwner.ForGuest(hash), Now, Guest);
        foreach (var (variant, quantity) in lines)
        {
            cart.AddLine(variant, quantity, 10, 50, Now, Guest);
        }

        return cart;
    }

    [Fact]
    [Trait("Spec", "CRT-3")]
    public async Task AGuestBasket_MovesIntoAnEmptyAccount_AndTheGuestBasketIsGone()
    {
        var key = NewGuestKey();
        await SaveAsync(GuestWith(key, (1, 2), (2, 1)));

        await MergeAsync(12_001, key);

        await using var db = postgres.NewContext();
        var account = await db.Carts.Include(c => c.Lines).SingleAsync(c => c.CustomerId == 12_001);
        Assert.Equal([(1L, 2), (2L, 1)], account.Lines.OrderBy(l => l.VariantId).Select(l => (l.VariantId, l.Quantity)));
        CartOwner.TryHashGuestKey(key, out var hash);
        Assert.Equal(0, await db.Carts.CountAsync(c => c.GuestKeyHash == hash));
    }

    [Fact]
    [Trait("Spec", "CRT-3")]
    public async Task AVariantInBothBaskets_BecomesOneLineWithTheQuantitiesAdded_NothingLostOrDuplicated()
    {
        var key = NewGuestKey();
        await SaveAsync(GuestWith(key, (1, 3), (3, 1)));
        var account = ShoppingCart.Create(CartOwner.ForCustomer(12_002), Now, Guest);
        account.AddLine(1, 2, 10, 50, Now, Guest);
        account.AddLine(2, 4, 10, 50, Now, Guest);
        await SaveAsync(account);

        await MergeAsync(12_002, key);

        await using var db = postgres.NewContext();
        var merged = await db.Carts.Include(c => c.Lines).SingleAsync(c => c.CustomerId == 12_002);
        Assert.Equal([(1L, 5), (2L, 4), (3L, 1)], merged.Lines.OrderBy(l => l.VariantId).Select(l => (l.VariantId, l.Quantity)));
    }

    [Fact]
    [Trait("Spec", "CRT-3")]
    public async Task AMergeNeverRaisesALineAboveTheMaximum()
    {
        var key = NewGuestKey();
        await SaveAsync(GuestWith(key, (1, 8)));
        var account = ShoppingCart.Create(CartOwner.ForCustomer(12_003), Now, Guest);
        account.AddLine(1, 7, 10, 50, Now, Guest);
        await SaveAsync(account);

        await MergeAsync(12_003, key);

        await using var db = postgres.NewContext();
        var merged = await db.Carts.Include(c => c.Lines).SingleAsync(c => c.CustomerId == 12_003);
        Assert.Equal(10, Assert.Single(merged.Lines).Quantity);
    }

    [Theory]
    [Trait("Spec", "CRT-9")]
    [InlineData("not-a-cookie")]
    [InlineData("")]
    [InlineData("' OR 1=1 --")]
    public async Task AValueWeNeverIssued_DoesNothing(string junk)
    {
        await MergeAsync(12_004, junk);

        await using var db = postgres.NewContext();
        Assert.Equal(0, await db.Carts.CountAsync(c => c.CustomerId == 12_004));
    }

    [Fact]
    [Trait("Spec", "CRT-10")]
    public async Task AnExpiredGuestBasket_IsDiscarded_NotMerged()
    {
        var key = NewGuestKey();
        await SaveAsync(GuestWith(key, (1, 2)));

        await MergeAsync(12_005, key, Now.AddDays(40));

        await using var db = postgres.NewContext();
        Assert.Equal(0, await db.Carts.CountAsync(c => c.CustomerId == 12_005));
        CartOwner.TryHashGuestKey(key, out var hash);
        Assert.Equal(0, await db.Carts.CountAsync(c => c.GuestKeyHash == hash));
    }

    [Fact]
    [Trait("Spec", "CRT-3")]
    public async Task ARepeatedMerge_IsHarmless()
    {
        var key = NewGuestKey();
        await SaveAsync(GuestWith(key, (1, 2)));

        await MergeAsync(12_006, key);
        await MergeAsync(12_006, key);

        await using var db = postgres.NewContext();
        Assert.Equal(2, Assert.Single((await db.Carts.Include(c => c.Lines).SingleAsync(c => c.CustomerId == 12_006)).Lines).Quantity);
    }

    [Fact]
    [Trait("Spec", "CRT-3")]
    public async Task TheSignInHook_IgnoresASignInWithNoGuestSession()
    {
        var sender = new RecordingSender();

        await new GuestCartMergeHook(sender).OnSignedInAsync(new SignedInContext(1, null), CancellationToken.None);
        await new GuestCartMergeHook(sender).OnSignedInAsync(new SignedInContext(1, string.Empty), CancellationToken.None);
        await new GuestCartMergeHook(sender).OnSignedInAsync(new SignedInContext(1, "abc"), CancellationToken.None);

        Assert.Equal(1, sender.Sent);
    }

    private sealed class RecordingSender : MediatR.ISender
    {
        public int Sent { get; private set; }

        public Task<TResponse> Send<TResponse>(MediatR.IRequest<TResponse> request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
            where TRequest : MediatR.IRequest
        {
            Sent++;
            return Task.CompletedTask;
        }

        public Task<object?> Send(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(MediatR.IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
