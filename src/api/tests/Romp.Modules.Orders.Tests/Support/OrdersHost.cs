using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Romp.BuildingBlocks.Application;
using Romp.BuildingBlocks.Persistence;
using Romp.BuildingBlocks.Persistence.Auditing;
using Romp.BuildingBlocks.Persistence.Inbox;
using Romp.BuildingBlocks.Persistence.Outbox;
using Romp.Modules.Cart.Contracts;
using Romp.Modules.Customers.Contracts;
using Microsoft.Extensions.Options;
using Romp.Modules.Orders.Application.Checkout;
using Romp.Modules.Orders.Application.Orders;
using Romp.Modules.Orders.Application;
using Romp.Modules.Orders.Application.Abstractions;
using Romp.Modules.Orders.Infrastructure.Orders;
using Romp.Modules.Orders.Infrastructure.Persistence;
using Romp.Modules.Reference.Contracts;
using Romp.Modules.Warehouse.Contracts;

namespace Romp.Modules.Orders.Tests.Support;

/// <summary>Cart's pricing as Orders sees it: a test sets exactly what the basket holds and costs.</summary>
internal sealed class FakePricing : ICheckoutPricing
{
    public string? CallerKey { get; set; } = "C1";

    public CheckoutPricing Priced { get; set; } = Basket();

    public static CheckoutPricing Basket(decimal discount = 0m, string? cartRef = "7", params (long Variant, int Quantity, decimal Price)[] lines)
    {
        var items = (lines.Length == 0 ? [(11L, 2, 1200m), (12L, 1, 500m)] : lines)
            .Select(l => new CheckoutLine(l.Variant, 500 + l.Variant, $"Dress {l.Variant}", "/girls/dresses/dress", $"ROMP-S{l.Variant}", "Red", "2-3Y", l.Price, l.Quantity, l.Price * l.Quantity, 9000 + l.Variant))
            .ToList();
        var subtotal = items.Sum(i => i.LineTotal);
        return new CheckoutPricing(items, subtotal, discount, 250m, true, subtotal - discount + 250m, cartRef, true, []);
    }

    public int PriceCalls { get; private set; }

    public Task<CheckoutPricing> PriceCurrentCartAsync(int? cityId, bool isCashOnDelivery, CancellationToken cancellationToken)
    {
        PriceCalls++;
        return Task.FromResult(Priced);
    }

    public Task<string?> GetCallerKeyAsync(CancellationToken cancellationToken) => Task.FromResult(CallerKey);
}

internal sealed class FakeRedemptions : IDiscountRedemptions
{
    public DiscountHold Hold { get; set; } = new(false, null, 0m, null);

    public List<string> Held { get; } = [];

    public List<string> Released { get; } = [];

    public Task<DiscountHold> HoldForCurrentCartAsync(string placementKey, string? guestMobileHash, CancellationToken cancellationToken)
    {
        Held.Add(placementKey);
        return Task.FromResult(Hold);
    }

    public Task ReleaseAsync(string placementKey, CancellationToken cancellationToken)
    {
        Released.Add(placementKey);
        return Task.CompletedTask;
    }
}

internal sealed class FakeReservations : IStockReservations
{
    public ReservationResult Result { get; set; } = new(ReservationOutcome.Reserved, 1, []);

    public List<(string Key, IReadOnlyList<ReservationLineRequest> Lines)> Reserved { get; } = [];

    public List<string> Released { get; } = [];

    public Task<ReservationResult> ReserveAsync(string placementKey, IReadOnlyList<ReservationLineRequest> lines, CancellationToken cancellationToken)
    {
        Reserved.Add((placementKey, lines));
        return Task.FromResult(Result);
    }

    public Task ReleaseAsync(string placementKey, CancellationToken cancellationToken)
    {
        Released.Add(placementKey);
        return Task.CompletedTask;
    }
}

internal sealed class FakeCustomer : ICurrentCustomerAccessor
{
    public long? CustomerId { get; set; }
}

internal sealed class FakeSavedAddresses : ICustomerAddressQueries
{
    public Dictionary<long, CustomerAddressSummary> Mine { get; } = [];

    public Task<IReadOnlyList<CustomerAddressSummary>> GetMyAddressesAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<CustomerAddressSummary>>([.. Mine.Values]);

    public Task<CustomerAddressSummary?> GetMyAddressAsync(long addressId, CancellationToken cancellationToken) =>
        Task.FromResult(Mine.TryGetValue(addressId, out var found) ? found : null);
}

internal sealed class FakeGeography : IGeographyQueries
{
    public Task<IReadOnlyList<ProvinceInfo>> GetProvincesAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ProvinceInfo>>([new(2, "PB", "Punjab"), new(1, "SD", "Sindh")]);

    public Task<IReadOnlyList<CityInfo>?> GetCitiesAsync(short provinceId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<CityInfo>?>(provinceId == 2 ? [new(7, "LHE", "Lahore", 2)] : null);

    public Task<bool> CityBelongsToProvinceAsync(short cityId, short provinceId, CancellationToken cancellationToken) => Task.FromResult(cityId == 7 && provinceId == 2);

    public Task<IReadOnlyDictionary<short, CityInfo>> GetCitiesByIdAsync(IReadOnlyCollection<short> ids, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<short, CityInfo>>(ids.Contains((short)7) ? new Dictionary<short, CityInfo> { [7] = new(7, "LHE", "Lahore", 2) } : new Dictionary<short, CityInfo>());

    public Task<IReadOnlyDictionary<short, ProvinceInfo>> GetProvincesByIdAsync(IReadOnlyCollection<short> ids, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<short, ProvinceInfo>>(ids.Contains((short)2) ? new Dictionary<short, ProvinceInfo> { [2] = new(2, "PB", "Punjab") } : new Dictionary<short, ProvinceInfo>());
}

/// <summary>
/// The Orders pipeline the module wires in production (validation, then the Orders transaction) against the real PostgreSQL, with the other
/// modules replaced by recording fakes: what is tested here is Orders' own placement logic and its SQL (ADR 0025).
/// </summary>
internal sealed class OrdersHost : IDisposable
{
    public static readonly DateTimeOffset Start = new(2026, 11, 3, 10, 0, 0, TimeSpan.Zero);

    private readonly ServiceProvider _root;

    public OrdersHost(OrdersPostgres postgres)
    {
        Pricing = new FakePricing();
        Redemptions = new FakeRedemptions();
        Reservations = new FakeReservations();
        Customer = new FakeCustomer { CustomerId = 1 };
        Addresses = new FakeSavedAddresses();
        Time = new FixedTime(Start);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSharedPersistence();
        services.AddScoped<OutboxMessageContext>();
        services.AddScoped<InboxGuard>();
        services.AddSingleton<TimeProvider>(Time);
        Options = new OrdersOptions();
        services.AddSingleton<IOptions<OrdersOptions>>(Microsoft.Extensions.Options.Options.Create(Options));
        services.AddSingleton<ICodLinks>(new FakeCodLinks());
        Captcha = new FakeCaptcha();
        services.AddSingleton<Romp.BuildingBlocks.Application.Captcha.ICaptchaVerifier>(Captcha);
        services.AddSingleton<Romp.Modules.Orders.Application.OrderLookup.ILookupAddressHasher>(new FakeAddressHasher());
        services.AddSingleton<Romp.Modules.Orders.Application.OrderLookup.ILookupLinks>(new FakeLookupLinks());
        services.AddSingleton<ICheckoutPricing>(Pricing);
        services.AddSingleton<IDiscountRedemptions>(Redemptions);
        services.AddSingleton<IStockReservations>(Reservations);
        services.AddSingleton<ICurrentCustomerAccessor>(Customer);
        services.AddSingleton<ICustomerAddressQueries>(Addresses);
        services.AddSingleton<IGeographyQueries>(new FakeGeography());
        services.AddDbContext<OrdrDbContext>((sp, options) =>
        {
            options.UseNpgsql(postgres.ConnectionString, npgsql => npgsql.MigrationsHistoryTable("MIG_HIST", "ORDR"));
            options.AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>(), sp.GetRequiredService<OutboxSaveChangesInterceptor>());
        });
        services.AddScoped<IOrdersDbContext>(sp => sp.GetRequiredService<OrdrDbContext>());
        services.AddScoped<IOrderNumberAllocator, OrderNumberAllocator>();
        services.AddSingleton(NullLoggerFactory.Instance);

        var applicationAssembly = typeof(Romp.Modules.Orders.Application.AssemblyReference).Assembly;
        services.AddValidatorsFromAssembly(applicationAssembly);
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(applicationAssembly);
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
            cfg.AddOpenBehavior(typeof(OrdersTransactionBehavior<,>));
        });
        _root = services.BuildServiceProvider();
    }

    public OrdersOptions Options { get; }

    public FakeCaptcha Captcha { get; }

    public FakePricing Pricing { get; }

    public FakeRedemptions Redemptions { get; }

    public FakeReservations Reservations { get; }

    public FakeCustomer Customer { get; }

    public FakeSavedAddresses Addresses { get; }

    public FixedTime Time { get; }

    public async Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request)
    {
        await using var scope = _root.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }

    public void Dispose() => _root.Dispose();
}

internal sealed class FixedTime(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;
}

/// <summary>Passes exactly the token <see cref="PassToken"/>, and counts how often it was asked.</summary>
internal sealed class FakeCaptcha : Romp.BuildingBlocks.Application.Captcha.ICaptchaVerifier
{
    public const string PassToken = "pass";

    public int Asked { get; private set; }

    public Task<bool> VerifyAsync(string? token, string? remoteIp, CancellationToken cancellationToken)
    {
        Asked++;
        return Task.FromResult(token == PassToken);
    }
}

/// <summary>The address, padded to the hash length, so a test can name its own callers.</summary>
internal sealed class FakeAddressHasher : Romp.Modules.Orders.Application.OrderLookup.ILookupAddressHasher
{
    public string Hash(string? clientIp) => (clientIp ?? string.Empty).PadRight(Romp.Modules.Orders.Domain.Orders.LookupAttempt.AddressHashLength, 'x');
}

internal sealed class FakeLookupLinks : Romp.Modules.Orders.Application.OrderLookup.ILookupLinks
{
    public string Create(string orderNumber) => $"trk-{orderNumber}";

    public string? TryRead(string? token) => token is not null && token.StartsWith("trk-", StringComparison.Ordinal) ? token[4..] : null;
}

/// <summary>Links made without a secret: a token is just the order number behind a prefix, so a test can build and break one.</summary>
internal sealed class FakeCodLinks : ICodLinks
{
    public string Create(string orderNumber, DateTimeOffset now) => $"tok-{orderNumber}";

    public string? TryRead(string? token, DateTimeOffset now) => token is not null && token.StartsWith("tok-", StringComparison.Ordinal) ? token[4..] : null;
}
