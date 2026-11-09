using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Persistence.Inbox;
using Romp.BuildingBlocks.Persistence.Outbox;
using Romp.Modules.Cart.Application.Carts;
using Romp.Modules.Cart.Application.DiscountCodes;
using Romp.Modules.Cart.Infrastructure.DiscountCodes;
using Romp.Modules.Cart.Tests.Support;
using Romp.Modules.Orders.Contracts;

namespace Romp.Modules.Cart.Tests.DiscountCodes;

/// <summary>
/// Orders' events become Cart commands, once (cart D6, DSC-10): a placed order confirms the discount use and empties the basket, a cancelled
/// order gives the code back, and a redelivered event (the same outbox message) does it only once, behind the inbox.
/// </summary>
public sealed class RedemptionEventHandlerTests(CartPostgres postgres) : IClassFixture<CartPostgres>
{
    private sealed class RecordingSender : ISender
    {
        public List<object> Sent { get; } = [];

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            Sent.Add(request);
            return Task.FromResult(default(TResponse)!);
        }

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
            where TRequest : IRequest
        {
            Sent.Add(request);
            return Task.CompletedTask;
        }

        public Task<object?> Send(object request, CancellationToken cancellationToken = default)
        {
            Sent.Add(request);
            return Task.FromResult<object?>(null);
        }

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private static long _message = 5000;

    private async Task DeliverPlacedAsync(RecordingSender sender, OrderPlacedEvent evt, long messageId)
    {
        await using var db = postgres.NewContext();
        var guard = new InboxGuard(new OutboxMessageContext { MessageId = messageId, Schema = "CART" }, TimeProvider.System);
        await new OrderPlacedCartConsumer(sender, db, guard).HandleAsync(evt, CancellationToken.None);
        await db.SaveChangesAsync();
    }

    private async Task DeliverCancelledAsync(RecordingSender sender, OrderCancelledEvent evt, long messageId)
    {
        await using var db = postgres.NewContext();
        var guard = new InboxGuard(new OutboxMessageContext { MessageId = messageId, Schema = "CART" }, TimeProvider.System);
        await new OrderCancelledCartConsumer(sender, db, guard).HandleAsync(evt, CancellationToken.None);
        await db.SaveChangesAsync();
    }

    [Fact]
    [Trait("Spec", "DSC-10")]
    public async Task APlacedOrder_BecomesOneConfirmCommand_WithItsKeyNumberAndBasket()
    {
        var sender = new RecordingSender();
        var id = Interlocked.Increment(ref _message);

        await DeliverPlacedAsync(sender, new OrderPlacedEvent(1, "RMP-2026-00001-4", "key-1", "COD", "77"), id);

        var command = Assert.IsType<ConfirmOrderPlacedCommand>(Assert.Single(sender.Sent));
        Assert.Equal(("key-1", "RMP-2026-00001-4", "77"), (command.PlacementKey, command.OrderNumber, command.CartRef));
    }

    [Fact]
    [Trait("Spec", "DSC-10")]
    public async Task ARedeliveredPlacedEvent_ConfirmsOnlyOnce()
    {
        var sender = new RecordingSender();
        var id = Interlocked.Increment(ref _message);
        var evt = new OrderPlacedEvent(2, "RMP-2026-00002-2", "key-2", "CARD", null);

        await DeliverPlacedAsync(sender, evt, id);
        await DeliverPlacedAsync(sender, evt, id);
        await DeliverPlacedAsync(sender, evt, id);

        Assert.Single(sender.Sent);
    }

    [Fact]
    [Trait("Spec", "DSC-10")]
    public async Task TwoDifferentMessages_AreBothHandled()
    {
        var sender = new RecordingSender();

        await DeliverPlacedAsync(sender, new OrderPlacedEvent(3, "RMP-2026-00003-0", "key-3", "COD", null), Interlocked.Increment(ref _message));
        await DeliverPlacedAsync(sender, new OrderPlacedEvent(4, "RMP-2026-00004-8", "key-4", "COD", null), Interlocked.Increment(ref _message));

        Assert.Equal(2, sender.Sent.Count);
    }

    [Fact]
    [Trait("Spec", "DSC-10")]
    public async Task ACancelledOrder_ReleasesTheCode_OnceEvenIfRedelivered()
    {
        var sender = new RecordingSender();
        var id = Interlocked.Increment(ref _message);
        var evt = new OrderCancelledEvent(5, "RMP-2026-00005-5", "key-5", "PYEX", false);

        await DeliverCancelledAsync(sender, evt, id);
        await DeliverCancelledAsync(sender, evt, id);

        var command = Assert.IsType<ReleaseRedemptionCommand>(Assert.Single(sender.Sent));
        Assert.Equal("key-5", command.PlacementKey);
    }

    [Fact]
    [Trait("Spec", "DSC-10")]
    public async Task ThePlacedAndCancelledHandlers_KeepSeparateInboxRows_ForTheSameMessage()
    {
        var sender = new RecordingSender();
        var id = Interlocked.Increment(ref _message);

        await DeliverPlacedAsync(sender, new OrderPlacedEvent(6, "RMP-2026-00006-3", "key-6", "COD", null), id);
        await DeliverCancelledAsync(sender, new OrderCancelledEvent(6, "RMP-2026-00006-3", "key-6", "CUST", false), id);

        Assert.Equal(2, sender.Sent.Count);
        await using var db = postgres.NewContext();
        Assert.Equal(2, await db.Set<InboxMessage>().CountAsync(m => m.MessageId == id));
    }
}
