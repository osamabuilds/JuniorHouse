using Microsoft.EntityFrameworkCore;
using Romp.Modules.Fulfilment.Domain;
using Romp.Modules.Fulfilment.Domain.Allocations;
using Romp.Modules.Fulfilment.Tests.Support;

namespace Romp.Modules.Fulfilment.Tests.Allocations;

/// <summary>S14 F1: the migration builds the FULF schema with its lookups, keys and constraints.</summary>
[Collection(FulfilmentPostgresFixtureDefinition.Name)]
public sealed class FulfilmentMigrationTests(FulfilmentPostgres postgres)
{
    [Fact]
    [Trait("Spec", "NFR-SC-01")]
    public async Task TheMigration_SeedsEveryLookup_WithTheCodesTheCodeUses()
    {
        await using var db = postgres.NewContext();

        Assert.Equal(["QUEUED", "PICKING", "PICKED", "PACKING", "PACKED", "HELD", "CANCELLED"], await db.Set<FulfilmentStatusLookup>().OrderBy(l => l.Id).Select(l => l.Code).ToListAsync());
        Assert.Equal(5, await db.Set<LineStatusLookup>().CountAsync());
        Assert.Equal(["NOT_FOUND", "SHORT", "DAMAGED"], await db.Set<ExceptionReasonLookup>().OrderBy(l => l.Id).Select(l => l.Code).ToListAsync());
        Assert.Equal(["POLY_BAG", "BOX"], await db.Set<PackagingKindLookup>().OrderBy(l => l.Id).Select(l => l.Code).ToListAsync());
        Assert.Equal(2, await db.Set<ScanStageLookup>().CountAsync());
        Assert.Equal(2, await db.Set<ScanOutcomeLookup>().CountAsync());
        Assert.Equal(2, await db.Set<ExceptionStatusLookup>().CountAsync());
    }

    [Fact]
    [Trait("Spec", "ALC-5")]
    public async Task TheDatabase_RefusesASecondFulfilmentOrderForTheSameOrder()
    {
        using var host = new FulfilmentHost(postgres);
        var placed = host.PlaceOrder(true, (1, "A-01-1", 5));
        await Seed.CreateAsync(host, placed.OrderId, postgres);
        await using var db = postgres.NewContext();
        var duplicate = FulfilmentOrder.Create(placed.OrderId, placed.OrderNumber, "other-key", true, 1m, FulfilmentHost.Start,
            [new FulfilmentLineSeed(1, "S", "n", "c", "s", 1, 1m)]);
        db.Orders.Add(duplicate);

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    [Trait("Spec", "PCK-5")]
    public async Task TheDatabase_RefusesAPickedQuantityAboveTheLineQuantity()
    {
        using var host = new FulfilmentHost(postgres);
        var placed = host.PlaceOrder(true, (1, "A-01-1", 5));
        await Seed.CreateAsync(host, placed.OrderId, postgres);
        await using var db = postgres.NewContext();

        var error = await Assert.ThrowsAnyAsync<Exception>(() =>
            db.Database.ExecuteSqlRawAsync("UPDATE \"FULF\".\"FULF_LINE\" SET \"PICK_QTY\" = \"QTY\" + 1 WHERE \"ORDR_ID\" IN (SELECT \"ID\" FROM \"FULF\".\"FULF_ORDR\" WHERE \"ORDR_ID\" = {0})", placed.OrderId));

        Assert.Contains("CK_FULF_LINE_PICK_QTY", error.ToString());
    }
}
