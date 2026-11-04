using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Application;
using Romp.BuildingBlocks.Domain;
using Romp.Modules.Cart.Application.Totals;
using Romp.Modules.Cart.Domain.Totals;
using Romp.Modules.Cart.Tests.Support;
using Romp.Modules.Reference.Contracts;

namespace Romp.Modules.Cart.Tests.Totals;

public sealed class ShippingTests(CartPostgres postgres) : IClassFixture<CartPostgres>
{
    private sealed class FakeGeography : IGeographyQueries
    {
        private readonly Dictionary<short, CityInfo> _cities = new()
        {
            [1] = new(1, "KAR", "Karachi", 1),
            [2] = new(2, "LHE", "Lahore", 2),
        };

        public Task<IReadOnlyList<ProvinceInfo>> GetProvincesAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<CityInfo>?> GetCitiesAsync(short provinceId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<bool> CityBelongsToProvinceAsync(short cityId, short provinceId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyDictionary<short, CityInfo>> GetCitiesByIdAsync(IReadOnlyCollection<short> ids, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<short, CityInfo>>(_cities.Where(c => ids.Contains(c.Key)).ToDictionary(c => c.Key, c => c.Value));

        public Task<IReadOnlyDictionary<short, ProvinceInfo>> GetProvincesByIdAsync(IReadOnlyCollection<short> ids, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<short, ProvinceInfo>>(
                new Dictionary<short, ProvinceInfo> { [1] = new(1, "SND", "Sindh"), [2] = new(2, "PNB", "Punjab") }.Where(p => ids.Contains(p.Key)).ToDictionary(p => p.Key, p => p.Value));
    }

    private async Task<T> RunAsync<T>(Func<Romp.Modules.Cart.Infrastructure.Persistence.CartDbContext, Task<T>> act)
    {
        await using var db = postgres.NewContext();
        var result = await act(db);
        await db.SaveChangesAsync();
        return result;
    }

    private Task<ShippingRateDto> SetRateAsync(short city, decimal fee) =>
        RunAsync(db => new SetShippingRateCommandHandler(db, new FakeGeography()).Handle(new SetShippingRateCommand(city, fee), CancellationToken.None));

    private Task<ShippingSettingsDto> SetSettingsAsync(decimal fee, decimal? free, decimal? cod) =>
        RunAsync(db => new SetShippingSettingsCommandHandler(db).Handle(new SetShippingSettingsCommand(fee, free, cod), CancellationToken.None));

    [Fact]
    [Trait("Spec", "TOT-5")]
    public async Task SettingARate_AddsItWithTheCityNames_AndSettingItAgainChangesTheSameRow()
    {
        var created = await SetRateAsync(1, 250m);
        var changed = await SetRateAsync(1, 300.555m);

        Assert.Equal(new ShippingRateDto(1, "Karachi", "Sindh", 250m), created);
        Assert.Equal(300.56m, changed.Fee);
        await using var db = postgres.NewContext();
        Assert.Equal(1, await db.ShippingRates.CountAsync(r => r.CityId == 1));
        Assert.Equal(300.56m, (await db.ShippingRates.SingleAsync(r => r.CityId == 1)).Fee);
    }

    [Fact]
    [Trait("Spec", "TOT-5")]
    public async Task ACityThatIsNotInTheReferenceData_IsRefused()
    {
        var ex = await Assert.ThrowsAsync<ValidationException>(() => SetRateAsync(999, 100m));

        Assert.Contains("not in the list of cities", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [Trait("Spec", "TOT-5")]
    [InlineData(-1)]
    [InlineData(100_001)]
    public async Task AFeeOutsideTheSaneRange_IsRefused(int fee)
    {
        await Assert.ThrowsAsync<DomainException>(() => SetRateAsync(2, fee));
    }

    [Fact]
    [Trait("Spec", "TOT-5")]
    public async Task RemovingARate_SendsTheCityBackToTheDefault_AndRemovingAnUnknownOneIsHarmless()
    {
        await SetRateAsync(2, 400m);

        await RunAsync(async db => { await new RemoveShippingRateCommandHandler(db).Handle(new RemoveShippingRateCommand(2), CancellationToken.None); return 0; });
        await RunAsync(async db => { await new RemoveShippingRateCommandHandler(db).Handle(new RemoveShippingRateCommand(2), CancellationToken.None); return 0; });

        await using var db = postgres.NewContext();
        Assert.Equal(0, await db.ShippingRates.CountAsync(r => r.CityId == 2));
    }

    [Fact]
    [Trait("Spec", "TOT-5")]
    public async Task TheList_IsPaged_WithNamesFromTwoBulkCalls()
    {
        await SetRateAsync(1, 200m);
        await SetRateAsync(2, 350m);

        await using var db = postgres.NewContext();
        var page = await new ListShippingRatesQueryHandler(db, new FakeGeography()).Handle(new ListShippingRatesQuery(1, 1), CancellationToken.None);

        Assert.Single(page.Items);
        Assert.True(page.TotalCount >= 2);
        Assert.Equal("Karachi", page.Items[0].CityName);
    }

    [Fact]
    [Trait("Spec", "TOT-5")]
    public async Task TheSettings_AreSavedValidatedAndNoLongerUnset()
    {
        var saved = await SetSettingsAsync(450m, 8000m, 100m);

        Assert.Equal(new ShippingSettingsDto(450m, 8000m, 100m, false), saved);
        await using var db = postgres.NewContext();
        var read = await new GetShippingSettingsQueryHandler(db).Handle(new GetShippingSettingsQuery(), CancellationToken.None);
        Assert.Equal(new ShippingSettingsDto(450m, 8000m, 100m, false), read);

        await Assert.ThrowsAsync<DomainException>(() => SetSettingsAsync(450m, 0m, null));
        await Assert.ThrowsAsync<DomainException>(() => SetSettingsAsync(-5m, null, null));
        await Assert.ThrowsAsync<DomainException>(() => SetSettingsAsync(450m, null, -1m));
    }

    [Fact]
    [Trait("Spec", "TOT-5")]
    public async Task ThePolicyLoader_ReadsTheCurrentRatesAndSettings_InTwoQueries_ForTheNextBasket()
    {
        await SetRateAsync(1, 220m);
        await SetSettingsAsync(500m, 9000m, 150m);

        await using var db = postgres.NewContext();
        var policy = await new ShippingPolicyLoader(db).LoadAsync(CancellationToken.None);

        Assert.Equal(220m, policy.FeeFor(1, 1000m, false));
        Assert.Equal(500m, policy.FeeFor(77, 1000m, false));
        Assert.Equal(650m, policy.FeeFor(77, 1000m, true));
        Assert.Equal(0m, policy.FeeFor(1, 9000m, false));

        await SetRateAsync(1, 260m);
        await using var later = postgres.NewContext();
        Assert.Equal(260m, (await new ShippingPolicyLoader(later).LoadAsync(CancellationToken.None)).FeeFor(1, 1000m, false));
    }

    [Fact]
    [Trait("Spec", "TOT-5")]
    public async Task TheDatabase_RefusesANegativeFeeOrASecondSettingsRow()
    {
        await using var db = postgres.NewContext();

        var negative = await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlRawAsync(
            "INSERT INTO \"CART\".\"SHPG_CITY_RATE\" (\"CITY_ID\", \"FEE_AMT\", \"INSR_DTE\", \"INSR_BY\") VALUES (5, -1, now(), 'test')"));
        var second = await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlRawAsync(
            "INSERT INTO \"CART\".\"SHPG_SETG\" (\"ID\", \"DFLT_FEE_AMT\", \"INSR_DTE\", \"INSR_BY\") VALUES (2, 0, now(), 'test')"));

        Assert.Contains("CK_SHPG_CITY_RATE_FEE", negative.ToString(), StringComparison.Ordinal);
        Assert.Contains("CK_SHPG_SETG_ONE", second.ToString(), StringComparison.Ordinal);
    }
}

public sealed class ShippingSeedTests(CartPostgres postgres) : IClassFixture<CartPostgres>
{
    [Fact]
    [Trait("Spec", "TOT-5")]
    public async Task TheSettingsRowExistsFromTheMigration_WithAnUnsetZeroDefault_AndTheQueryFlagsIt()
    {
        await using var db = postgres.NewContext();

        var fresh = await db.ShippingSettings.AsNoTracking().SingleAsync();
        var dto = await new GetShippingSettingsQueryHandler(db).Handle(new GetShippingSettingsQuery(), CancellationToken.None);

        Assert.Equal((0m, null, null), (fresh.DefaultFee, fresh.FreeThreshold, fresh.CodFee));
        Assert.True(dto.DefaultFeeIsUnset);
    }
}

public sealed class CartTotalsCalculatorTests(CartPostgres postgres) : IClassFixture<CartPostgres>
{
    private static class Line
    {
        public static Romp.Modules.Cart.Application.Carts.PricedLine Make(long variant, decimal unitPrice, int quantity, Romp.Modules.Cart.Application.Carts.LineIssues issues = Romp.Modules.Cart.Application.Carts.LineIssues.None) =>
            new(variant, 1, "Dress", "/p", "Red", "2Y", "S", issues.HasFlag(Romp.Modules.Cart.Application.Carts.LineIssues.Unavailable) ? null : unitPrice, null, quantity,
                issues.HasFlag(Romp.Modules.Cart.Application.Carts.LineIssues.Unavailable) ? 0m : unitPrice * quantity, 9, null, issues, null, null, null);
    }

    private async Task<Romp.Modules.Cart.Domain.Totals.OrderAmounts> CalcAsync(Romp.Modules.Cart.Application.Carts.PricedCart cart, int? city, bool cod, decimal discount = 0m)
    {
        await using var db = postgres.NewContext();
        if (!await db.ShippingRates.AnyAsync(r => r.CityId == 5))
        {
            db.ShippingRates.Add(new ShippingCityRate(5, 200m));
        }

        db.ShippingSettings.Single().Set(500m, 8000m, 100m);
        await db.SaveChangesAsync();
        await using var read = postgres.NewContext();
        return await new Romp.Modules.Cart.Application.Totals.CartTotalsCalculator(new ShippingPolicyLoader(read)).CalculateAsync(cart, city, cod, discount, CancellationToken.None);
    }

    private static Romp.Modules.Cart.Application.Carts.PricedCart CartOf(params Romp.Modules.Cart.Application.Carts.PricedLine[] lines) =>
        new(lines, lines.Sum(l => l.Quantity), lines.Where(l => l.UnitPrice is not null).Sum(l => l.LineTotal));

    [Fact]
    [Trait("Spec", "TOT-1")]
    public async Task WithACity_DeliveryIsThatCitysRate_ChargedOnce_AndTheTotalIsExact()
    {
        var amounts = await CalcAsync(CartOf(Line.Make(1, 1000m, 2), Line.Make(2, 500m, 1)), 5, cod: false);

        Assert.Equal(2500m, amounts.Subtotal);
        Assert.Equal(200m, amounts.Shipping);
        Assert.True(amounts.ShippingKnown);
        Assert.Equal(2700m, amounts.Total);
    }

    [Fact]
    [Trait("Spec", "TOT-4")]
    public async Task WithNoCity_DeliveryIsNotKnown_AndNotInTheTotal()
    {
        var amounts = await CalcAsync(CartOf(Line.Make(1, 1000m, 1)), null, cod: false);

        Assert.False(amounts.ShippingKnown);
        Assert.Equal(1000m, amounts.Total);
    }

    [Fact]
    [Trait("Spec", "TOT-1")]
    public async Task ACityWithNoRate_UsesTheDefault_AndCodAddsItsFee()
    {
        var plain = await CalcAsync(CartOf(Line.Make(1, 1000m, 1)), 99, cod: false);
        var cod = await CalcAsync(CartOf(Line.Make(1, 1000m, 1)), 99, cod: true);

        Assert.Equal(500m, plain.Shipping);
        Assert.Equal(600m, cod.Shipping);
    }

    [Fact]
    [Trait("Spec", "TOT-1")]
    public async Task DeliveryIsFree_WhenTheSubtotalAfterTheDiscountReachesTheThreshold()
    {
        var free = await CalcAsync(CartOf(Line.Make(1, 4500m, 2)), 5, cod: false);
        var notFree = await CalcAsync(CartOf(Line.Make(1, 4500m, 2)), 5, cod: false, discount: 1500m);

        Assert.Equal(0m, free.Shipping);
        Assert.Equal(200m, notFree.Shipping);
        Assert.Equal(7700m, notFree.Total);
    }

    [Fact]
    [Trait("Spec", "TOT-4")]
    public async Task LinesThatCannotBeBought_AreLeftOut_AndAnEmptyBasketIsNotCharged()
    {
        var gone = await CalcAsync(CartOf(Line.Make(1, 1000m, 1), Line.Make(2, 0m, 3, Romp.Modules.Cart.Application.Carts.LineIssues.Unavailable)), 5, cod: false);
        var empty = await CalcAsync(Romp.Modules.Cart.Application.Carts.PricedCart.Empty, 5, cod: false);

        Assert.Equal(1000m, gone.Subtotal);
        Assert.Equal(new Romp.Modules.Cart.Domain.Totals.OrderAmounts(0m, 0m, 0m, true, 0m), empty with { ShippingKnown = true });
        Assert.Equal(0m, empty.Total);
    }
}
