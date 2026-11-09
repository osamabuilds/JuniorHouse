using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Romp.Modules.Orders.Application.Orders;
using Romp.Modules.Orders.Domain.Orders;
using Romp.Modules.Orders.Infrastructure.Persistence;
using Romp.Modules.Orders.Tests.Support;

namespace Romp.Modules.Orders.Tests.Orders;

/// <summary>A signed-in customer reads only their own orders, paged, in a constant number of queries (orders P8, ORD-13, ORD-14, SEC-05).</summary>
public sealed class MyOrdersTests(OrdersPostgres postgres) : IClassFixture<OrdersPostgres>
{
    private sealed class CountingInterceptor : DbCommandInterceptor
    {
        public int Commands { get; private set; }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Commands++;
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }

    private static string Key() => Guid.NewGuid().ToString("N");

    private static PlaceOrderCommand Place(short method = PaymentMethods.CashOnDelivery) =>
        new(Key(), new CheckoutContact("Ayesha Khan", "ayesha@example.com", "0300 1234567", false),
            new CheckoutAddress("Ayesha Khan", "0300 1234567", "House 4, Street 2", null, 2, 7, null, null), null, method);

    private static async Task<List<string>> PlaceAsAsync(OrdersHost host, long? customerId, int count, short method = PaymentMethods.CashOnDelivery)
    {
        host.Customer.CustomerId = customerId;
        var numbers = new List<string>();
        for (var i = 0; i < count; i++)
        {
            numbers.Add((await host.SendAsync(Place(method))).OrderNumber!);
        }

        return numbers;
    }

    private (OrdrDbContext Db, CountingInterceptor Counter) Counting()
    {
        var counter = new CountingInterceptor();
        var options = new DbContextOptionsBuilder<OrdrDbContext>()
            .UseNpgsql(postgres.ConnectionString, npgsql => npgsql.MigrationsHistoryTable("MIG_HIST", "ORDR"))
            .AddInterceptors(counter)
            .Options;
        return (new OrdrDbContext(options), counter);
    }

    [Fact]
    [Trait("Spec", "ORD-13")]
    [Trait("Spec", "ORD-14")]
    public async Task TheList_HoldsOnlyTheCustomersOwnOrders_NewestFirst_AndIsPaged()
    {
        using var host = new OrdersHost(postgres);
        var mine = await PlaceAsAsync(host, 4001, 7);
        var theirs = await PlaceAsAsync(host, 4002, 3);
        var guest = await PlaceAsAsync(host, null, 2);
        var (db, _) = Counting();
        await using var dbScope = db;
        var handler = new ListMyOrdersQueryHandler(db, new FakeCustomer { CustomerId = 4001 });

        var first = await handler.Handle(new ListMyOrdersQuery(1, 5), CancellationToken.None);
        var second = await handler.Handle(new ListMyOrdersQuery(2, 5), CancellationToken.None);

        Assert.Equal((7, 5, 2), (first.TotalCount, first.Items.Count, second.Items.Count));
        var all = first.Items.Concat(second.Items).Select(i => i.OrderNumber).ToList();
        Assert.Equal(mine.AsEnumerable().Reverse(), all);
        Assert.Empty(all.Intersect(theirs));
        Assert.Empty(all.Intersect(guest));
        Assert.All(first.Items, i => Assert.Equal(("Pending confirmation", "Cash on Delivery", 3, 3150m), (i.StatusName, i.MethodName, i.ItemCount, i.Total)));
    }

    [Fact]
    [Trait("Spec", "ORD-14")]
    public async Task TheList_TakesTheSameFewQueries_WhateverThePageSizeOrTheNumberOfOrders()
    {
        using var host = new OrdersHost(postgres);
        await PlaceAsAsync(host, 4101, 12);
        var (db, counter) = Counting();
        await using var dbScope = db;
        var handler = new ListMyOrdersQueryHandler(db, new FakeCustomer { CustomerId = 4101 });

        await handler.Handle(new ListMyOrdersQuery(1, 3), CancellationToken.None);
        var small = counter.Commands;
        await handler.Handle(new ListMyOrdersQuery(1, 12), CancellationToken.None);
        var large = counter.Commands - small;

        Assert.Equal(2, small);
        Assert.Equal(2, large);
    }

    [Fact]
    [Trait("Spec", "ORD-13")]
    public async Task AnotherCustomersOrder_AGuestsOrder_AndAnUnknownNumber_AreAllJustNotFound()
    {
        using var host = new OrdersHost(postgres);
        var mine = (await PlaceAsAsync(host, 4201, 1))[0];
        var theirs = (await PlaceAsAsync(host, 4202, 1))[0];
        var guest = (await PlaceAsAsync(host, null, 1))[0];
        var (db, _) = Counting();
        await using var dbScope = db;
        var asMine = new GetMyOrderQueryHandler(db, new FakeCustomer { CustomerId = 4201 });

        Assert.NotNull(await asMine.Handle(new GetMyOrderQuery(mine), CancellationToken.None));
        Assert.Null(await asMine.Handle(new GetMyOrderQuery(theirs), CancellationToken.None));
        Assert.Null(await asMine.Handle(new GetMyOrderQuery(guest), CancellationToken.None));
        Assert.Null(await asMine.Handle(new GetMyOrderQuery("RMP-2026-99999-9"), CancellationToken.None));
        Assert.Null(await asMine.Handle(new GetMyOrderQuery("' OR 1=1 --"), CancellationToken.None));
        Assert.Null(await asMine.Handle(new GetMyOrderQuery(null), CancellationToken.None));
    }

    [Fact]
    [Trait("Spec", "ORD-13")]
    public async Task AGuestSession_ReadsNothing()
    {
        using var host = new OrdersHost(postgres);
        await PlaceAsAsync(host, 4301, 2);
        var (db, _) = Counting();
        await using var dbScope = db;

        var list = await new ListMyOrdersQueryHandler(db, new FakeCustomer { CustomerId = null }).Handle(new ListMyOrdersQuery(), CancellationToken.None);
        var one = await new GetMyOrderQueryHandler(db, new FakeCustomer { CustomerId = null }).Handle(new GetMyOrderQuery("RMP-2026-00001-4"), CancellationToken.None);

        Assert.Empty(list.Items);
        Assert.Equal(0, list.TotalCount);
        Assert.Null(one);
    }

    [Fact]
    [Trait("Spec", "ORD-13")]
    public async Task OneOrder_ShowsItsLines_ItsTimeline_AndNoContactDetails_InAFewQueries()
    {
        using var host = new OrdersHost(postgres);
        var number = (await PlaceAsAsync(host, 4401, 1))[0];
        var (db, counter) = Counting();
        await using var dbScope = db;

        var order = await new GetMyOrderQueryHandler(db, new FakeCustomer { CustomerId = 4401 }).Handle(new GetMyOrderQuery(number.ToLowerInvariant()), CancellationToken.None);

        Assert.NotNull(order);
        Assert.Equal((number, 2900m, 250m, 3150m), (order.OrderNumber, order.Subtotal, order.Shipping, order.Total));
        Assert.Equal(2, order.Lines.Count);
        Assert.Equal("Pending confirmation", Assert.Single(order.Timeline).StatusName);
        Assert.Equal(("Lahore", "Punjab"), (order.CityName, order.ProvinceName));
        Assert.True(counter.Commands <= 4, $"{counter.Commands} queries");
        var shape = string.Join(',', typeof(MyOrderDetailDto).GetProperties().Select(p => p.Name));
        Assert.DoesNotContain("Mobile", shape, StringComparison.Ordinal);
        Assert.DoesNotContain("Phone", shape, StringComparison.Ordinal);
        Assert.DoesNotContain("Email", shape, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Spec", "ORD-14")]
    public async Task APageSizeBeyondTheLimit_IsClamped_NotRejected_AndANegativePageIsTheFirst()
    {
        using var host = new OrdersHost(postgres);
        await PlaceAsAsync(host, 4501, 3);
        var (db, _) = Counting();
        await using var dbScope = db;
        var handler = new ListMyOrdersQueryHandler(db, new FakeCustomer { CustomerId = 4501 });

        var huge = await handler.Handle(new ListMyOrdersQuery(1, 100_000), CancellationToken.None);
        var negative = await handler.Handle(new ListMyOrdersQuery(-5, -1), CancellationToken.None);

        Assert.Equal(100, huge.PageSize);
        Assert.Equal((1, 3), (negative.Page, negative.TotalCount));
        Assert.Equal(1, negative.PageSize);
    }
}
