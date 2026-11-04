using Romp.BuildingBlocks.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Romp.Modules.Identity.Application.AuditTrail;
using Romp.Modules.Identity.Domain.AuditTrail;
using Romp.Modules.Identity.Tests.Roles;
using Romp.Modules.Identity.Tests.Support;

namespace Romp.Modules.Identity.Tests.AuditTrail;

/// <summary>The audit list and detail (identity/audit-trail AC-8, task 10): filters, paging, the range cap, and a constant number of queries.</summary>
[Collection(IdentityPostgresFixtureDefinition.Name)]
public sealed class AuditQueryTests(IdentityPostgres postgres)
{
    private static long _seed = 5_000_000;

    /// <summary>Stores entries for a fresh actor name so the test owns its rows although the database is shared.</summary>
    private async Task<string> SeedAsync(int count, Func<int, (string Type, string Key, AuditOperation Op, DateTimeOffset At)>? shape = null)
    {
        var actor = $"seed.{Interlocked.Increment(ref _seed)}";
        await using var context = postgres.NewContext();
        await context.Database.ExecuteSqlAsync($"""SELECT "IDNT"."AUDT_PART_ENSR"({new DateOnly(IdentityHost.Start.Year, IdentityHost.Start.Month, 1).AddMonths(-1)}, 3)""");
        for (var i = 0; i < count; i++)
        {
            var (type, key, op, at) = shape?.Invoke(i) ?? ("STYL_MAIN", (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture), AuditOperation.Update, IdentityHost.Start.AddMinutes(-i));
            context.AuditEntries.Add(AuditEntry.Create(at, "CTLG", Interlocked.Increment(ref _seed), type, key, op, actor, "trace", ["NAME", "SLUG"]));
        }

        await context.SaveChangesAsync();
        return actor;
    }

    private IdentityHost NewHost(CommandCounter? counter = null) => new(postgres, counter);

    [Fact]
    [Trait("Spec", "AC-8")]
    public async Task FiltersAndPages_InOneQuery()
    {
        var actor = await SeedAsync(30, i => (i % 3 == 0 ? "PROD_MAIN" : "STYL_MAIN", i.ToString(System.Globalization.CultureInfo.InvariantCulture), i % 5 == 0 ? AuditOperation.Insert : AuditOperation.Update, IdentityHost.Start.AddMinutes(-i)));
        var counter = new CommandCounter();
        using var host = NewHost(counter);

        counter.Reset();
        var firstPage = await host.SendAsync(new ListAuditQuery(Actor: actor, PageSize: 10));

        Assert.Equal(30, firstPage.TotalCount);
        Assert.Equal(10, firstPage.Items.Count);
        Assert.True(counter.Count <= 2, $"expected a COUNT and one page query, saw {counter.Count}"); // no N+1 over the column names
        Assert.Equal(firstPage.Items.OrderByDescending(e => e.OccurredAt).Select(e => e.Id), firstPage.Items.Select(e => e.Id)); // newest first
        Assert.All(firstPage.Items, e => Assert.Equal(["NAME", "SLUG"], e.Columns));

        var third = await host.SendAsync(new ListAuditQuery(Actor: actor, PageSize: 10, Page: 3));
        Assert.Equal(10, third.Items.Count);
        Assert.Empty(firstPage.Items.Select(e => e.Id).Intersect(third.Items.Select(e => e.Id)));

        var products = await host.SendAsync(new ListAuditQuery(Actor: actor, EntityType: "PROD_MAIN", PageSize: 100));
        Assert.Equal(10, products.TotalCount);

        var inserts = await host.SendAsync(new ListAuditQuery(Actor: actor, Operation: "insert", PageSize: 100));
        Assert.Equal(6, inserts.TotalCount);
        Assert.All(inserts.Items, e => Assert.Equal("INSERT", e.Operation));

        var oneRecord = await host.SendAsync(new ListAuditQuery(Actor: actor, EntityType: "STYL_MAIN", EntityKey: "1"));
        Assert.Equal("1", Assert.Single(oneRecord.Items).EntityKey);

        var module = await host.SendAsync(new ListAuditQuery(Actor: actor, Module: "WHSE"));
        Assert.Equal(0, module.TotalCount);
    }

    [Fact]
    [Trait("Spec", "AC-8")]
    public async Task DateRange_NarrowsTheList()
    {
        var actor = await SeedAsync(10);
        using var host = NewHost();

        var recent = await host.SendAsync(new ListAuditQuery(Actor: actor, From: IdentityHost.Start.AddMinutes(-4), To: IdentityHost.Start));

        Assert.Equal(5, recent.TotalCount);
    }

    [Fact]
    [Trait("Spec", "AC-8")]
    public async Task RangeOver366Days_IsRejected()
    {
        using var host = NewHost();

        await Assert.ThrowsAsync<ValidationException>(() => host.SendAsync(new ListAuditQuery(From: IdentityHost.Start.AddDays(-400), To: IdentityHost.Start)));
        await Assert.ThrowsAsync<ValidationException>(() => host.SendAsync(new ListAuditQuery(From: IdentityHost.Start, To: IdentityHost.Start.AddDays(-1))));
        await Assert.ThrowsAsync<ValidationException>(() => host.SendAsync(new ListAuditQuery(PageSize: 101)));
        await Assert.ThrowsAsync<ValidationException>(() => host.SendAsync(new ListAuditQuery(Operation: "EXPLODE")));
    }

    [Fact]
    [Trait("Spec", "AC-8")]
    public async Task Detail_ReturnsTheEntry_AndNotFoundForAMissingId()
    {
        var actor = await SeedAsync(1);
        using var host = NewHost();
        var listed = (await host.SendAsync(new ListAuditQuery(Actor: actor))).Items.Single();

        var detail = await host.SendAsync(new GetAuditEntryQuery(listed.Id));

        Assert.Equal(listed, detail with { Columns = detail.Columns }, new EntryComparer());
        await Assert.ThrowsAsync<KeyNotFoundException>(() => host.SendAsync(new GetAuditEntryQuery(long.MaxValue)));
    }

    private sealed class EntryComparer : IEqualityComparer<AuditEntryDto>
    {
        public bool Equals(AuditEntryDto? x, AuditEntryDto? y) =>
            x is not null && y is not null && x.Id == y.Id && x.Actor == y.Actor && x.EntityKey == y.EntityKey && x.Columns.SequenceEqual(y.Columns);

        public int GetHashCode(AuditEntryDto obj) => obj.Id.GetHashCode();
    }
}
