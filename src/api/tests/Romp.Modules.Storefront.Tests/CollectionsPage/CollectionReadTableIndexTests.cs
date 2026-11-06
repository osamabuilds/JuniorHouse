using Microsoft.EntityFrameworkCore;
using Romp.Modules.Storefront.Domain.PublishedCatalog;
using Romp.Modules.Storefront.Infrastructure.Persistence;

namespace Romp.Modules.Storefront.Tests.CollectionsPage;

/// <summary>
/// Collections-page task C1: the collection read table has an index on the collection code (the collection listing and the index's grouped
/// query) and one that starts with the product id (the cascade from a product and the per-product lookups), so neither scans the table.
/// </summary>
public sealed class CollectionReadTableIndexTests
{
    private static List<string[]> IndexedColumns()
    {
        // Reading the model needs no connection, so a placeholder connection string is enough.
        using var context = new StfrDbContext(new DbContextOptionsBuilder<StfrDbContext>().UseNpgsql("Host=unused").Options);
        var entity = context.Model.FindEntityType(typeof(CollectionRow))!;
        return entity.GetIndexes().Select(i => i.Properties.Select(p => p.GetColumnName()).ToArray()).ToList();
    }

    [Fact]
    [Trait("Spec", "COL-1")]
    public void TheCollectionTable_IsIndexedOnTheCollectionCode()
    {
        Assert.Contains(IndexedColumns(), columns => columns.SequenceEqual(["COLN_CODE"]));
    }

    [Fact]
    [Trait("Spec", "COL-1")]
    public void TheCollectionTable_HasAnIndexLedByTheProductId()
    {
        Assert.Contains(IndexedColumns(), columns => columns[0] == "PRDT_ID");
    }
}
