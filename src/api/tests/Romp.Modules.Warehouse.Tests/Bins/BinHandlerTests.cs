using Romp.BuildingBlocks.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Romp.BuildingBlocks.Domain;
using Romp.Modules.Warehouse.Application.Bins;
using Romp.Modules.Warehouse.Tests.Support;

namespace Romp.Modules.Warehouse.Tests.Bins;

[Collection(WarehousePostgresFixtureDefinition.Name)]
public sealed class BinHandlerTests(WarehousePostgres postgres)
{
    private const string Person = "Ayesha Khan";

    [Fact]
    [Trait("Spec", "BIN-1")]
    [Trait("Spec", "BIN-7")]
    public async Task ANewBin_GetsItsCode_IsListed_AndIsAttributable()
    {
        using var host = new WarehouseHost(postgres);
        var zone = WarehouseHost.NewZone();

        var created = await host.SendAsync(new CreateBinCommand(zone.ToLowerInvariant(), "3", "b", Person));

        Assert.Equal($"{zone}-3-B", created.BinCode);
        Assert.True(created.IsActive);
        var page = await host.SendAsync(new SearchBinsQuery(zone));
        Assert.Equal(created.Id, Assert.Single(page.Items).Id);

        await using var db = postgres.NewContext();
        var row = await db.Bins.AsNoTracking().SingleAsync(b => b.Id == created.Id);
        Assert.NotEqual(default, row.InsrDte);
        Assert.False(string.IsNullOrWhiteSpace(row.InsrBy));
    }

    [Fact]
    [Trait("Spec", "BIN-2")]
    public async Task ASecondBinWithTheSameCode_IsRefused_AndNothingIsStored()
    {
        using var host = new WarehouseHost(postgres);
        var zone = WarehouseHost.NewZone();
        await host.SendAsync(new CreateBinCommand(zone, "01", "A", Person));

        var ex = await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(new CreateBinCommand(zone, "1".PadLeft(2, '0'), "a", Person)));

        Assert.Contains("already exists", ex.Message, StringComparison.Ordinal);
        Assert.Equal(1, (await host.SendAsync(new SearchBinsQuery(zone))).TotalCount);
    }

    [Fact]
    [Trait("Spec", "BIN-2")]
    public async Task TenParallelCreatesOfTheSameCode_LeaveExactlyOneBin()
    {
        using var host = new WarehouseHost(postgres);
        var zone = WarehouseHost.NewZone();

        var results = await Task.WhenAll(Enumerable.Range(0, 10).Select(async _ =>
        {
            try
            {
                await host.SendAsync(new CreateBinCommand(zone, "07", "Z", Person));
                return true;
            }
            catch (Exception ex) when (ex is DomainException or DbUpdateException or PostgresException)
            {
                return false;
            }
        }));

        Assert.Equal(1, results.Count(created => created));
        Assert.Equal(1, (await host.SendAsync(new SearchBinsQuery(zone))).TotalCount);
    }

    [Theory]
    [InlineData("", "01", "A")]
    [InlineData("TOOLONG", "01", "A")]
    [InlineData("A", "x", "A")]
    [InlineData("A'; DROP TABLE", "01", "A")]
    [InlineData("A", "01", "<b>")]
    [Trait("Spec", "BIN-3")]
    public async Task InvalidParts_AreRefused_AndNothingIsStored(string zone, string aisle, string shelf)
    {
        using var host = new WarehouseHost(postgres);
        var before = (await host.SendAsync(new SearchBinsQuery())).TotalCount;

        await Assert.ThrowsAnyAsync<Exception>(() => host.SendAsync(new CreateBinCommand(zone, aisle, shelf, Person)));

        Assert.Equal(before, (await host.SendAsync(new SearchBinsQuery())).TotalCount);
    }

    [Theory]
    [InlineData("")]
    [InlineData("x")]
    [InlineData("Line\nBreak")]
    [Trait("Spec", "BIN-7")]
    public async Task TheActingPerson_IsRequired_AndCannotCarryControlCharacters(string person)
    {
        using var host = new WarehouseHost(postgres);

        await Assert.ThrowsAsync<ValidationException>(() => host.SendAsync(new CreateBinCommand(WarehouseHost.NewZone(), "01", "A", person)));
    }

    [Fact]
    [Trait("Spec", "BIN-4")]
    public async Task ABinWithStock_CannotBeDeactivated_ButAnEmptyOneCan_AndComeBack()
    {
        using var host = new WarehouseHost(postgres);
        var bin = await host.SendAsync(new CreateBinCommand(WarehouseHost.NewZone(), "01", "A", Person));
        host.BinStock.Units[bin.Id] = 5;

        await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(new SetBinActiveCommand(bin.Id, false, Person)));
        Assert.True((await host.SendAsync(new GetBinByCodeQuery(bin.BinCode)))!.IsActive);

        host.BinStock.Units[bin.Id] = 0;
        Assert.False((await host.SendAsync(new SetBinActiveCommand(bin.Id, false, Person))).IsActive);
        Assert.True((await host.SendAsync(new SetBinActiveCommand(bin.Id, true, Person))).IsActive);
    }

    [Fact]
    [Trait("Spec", "BIN-4")]
    public async Task SettingTheStateABinAlreadyHas_ChangesNothing()
    {
        using var host = new WarehouseHost(postgres);
        var bin = await host.SendAsync(new CreateBinCommand(WarehouseHost.NewZone(), "01", "A", Person));
        host.BinStock.Units[bin.Id] = 9;

        var again = await host.SendAsync(new SetBinActiveCommand(bin.Id, true, Person));

        Assert.True(again.IsActive);
        Assert.Equal(9, again.UnitsOnHand);
    }

    [Fact]
    [Trait("Spec", "BIN-4")]
    public async Task AnUnknownBin_IsNotFound() =>
        await Assert.ThrowsAsync<KeyNotFoundException>(() => new WarehouseHost(postgres).SendAsync(new SetBinActiveCommand(long.MaxValue - 1, false, Person)));

    [Fact]
    [Trait("Spec", "BIN-5")]
    public async Task TheReader_FindsBinsByExactCode_AndReportsWhetherTheyAreActive()
    {
        using var host = new WarehouseHost(postgres);
        var bin = await host.SendAsync(new CreateBinCommand(WarehouseHost.NewZone(), "02", "C", Person));
        await host.SendAsync(new SetBinActiveCommand(bin.Id, false, Person));

        var found = await host.InScopeAsync(sp => sp.GetRequiredService<IBinReader>().FindByCodeAsync(bin.BinCode.ToLowerInvariant(), default));
        var missing = await host.InScopeAsync(sp => sp.GetRequiredService<IBinReader>().FindByCodeAsync("NO-1-X", default));

        Assert.NotNull(found);
        Assert.False(found.IsActive);
        Assert.Null(missing);
    }

    [Fact]
    [Trait("Spec", "BIN-6")]
    public async Task TheList_IsPaged_OrderedByAUniqueKey_AndClampsThePageSize()
    {
        using var host = new WarehouseHost(postgres);
        var zone = WarehouseHost.NewZone();
        for (var i = 1; i <= 5; i++)
        {
            await host.SendAsync(new CreateBinCommand(zone, i.ToString("00", System.Globalization.CultureInfo.InvariantCulture), "A", Person));
        }

        var first = await host.SendAsync(new SearchBinsQuery(zone, 1, 2));
        var second = await host.SendAsync(new SearchBinsQuery(zone, 2, 2));
        var huge = await host.SendAsync(new SearchBinsQuery(zone, 1, 10_000));

        Assert.Equal(5, first.TotalCount);
        Assert.Equal(2, first.Items.Count);
        Assert.Empty(first.Items.Select(b => b.Id).Intersect(second.Items.Select(b => b.Id)));
        Assert.True(first.Items[0].Id < first.Items[1].Id);
        Assert.Equal(100, huge.PageSize);
    }

    [Fact]
    [Trait("Spec", "BIN-6")]
    public async Task TheList_RunsAConstantNumberOfQueries_WhateverThePageHolds()
    {
        var counter = new CommandCounter();
        using var host = new WarehouseHost(postgres, counter: counter);
        var smallZone = WarehouseHost.NewZone();
        var bigZone = WarehouseHost.NewZone();
        await host.SendAsync(new CreateBinCommand(smallZone, "01", "A", Person));
        for (var i = 1; i <= 20; i++)
        {
            await host.SendAsync(new CreateBinCommand(bigZone, i.ToString("00", System.Globalization.CultureInfo.InvariantCulture), "A", Person));
        }

        counter.Reset();
        await host.SendAsync(new SearchBinsQuery(smallZone));
        var small = counter.Count;
        counter.Reset();
        await host.SendAsync(new SearchBinsQuery(bigZone));

        Assert.Equal(small, counter.Count);
    }

    [Fact]
    [Trait("Spec", "BIN-6")]
    public async Task TheListShowsUnitsOnHand_FromTheStockPort()
    {
        using var host = new WarehouseHost(postgres);
        var bin = await host.SendAsync(new CreateBinCommand(WarehouseHost.NewZone(), "01", "A", Person));
        host.BinStock.Units[bin.Id] = 12;

        var row = Assert.Single((await host.SendAsync(new SearchBinsQuery(bin.Zone))).Items);

        Assert.Equal(12, row.UnitsOnHand);
    }
}
