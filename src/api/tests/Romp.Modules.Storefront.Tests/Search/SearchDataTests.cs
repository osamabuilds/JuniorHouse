using Microsoft.EntityFrameworkCore;
using Romp.Modules.Storefront.Application.PublishedCatalog;
using Romp.Modules.Storefront.Tests.Support;

namespace Romp.Modules.Storefront.Tests.Search;

/// <summary>Search tasks 1 and 2: the search columns, indexes and seed data against a real PostgreSQL, and the projection that fills them (SRC-8, 12).</summary>
[Collection(StorefrontPostgresFixtureDefinition.Name)]
public sealed class SearchDataTests(StorefrontPostgres postgres)
{
    private static Task<T> ScalarAsync<T>(Romp.Modules.Storefront.Infrastructure.Persistence.StfrDbContext db, string sql, params object[] args) =>
        db.Database.SqlQueryRaw<T>(sql, args).SingleAsync();

    [Fact]
    [Trait("Spec", "SRC-8")]
    public async Task Apply_StoresTheWordsTheProductCanBeFoundBy()
    {
        using var host = await StorefrontHost.CreateAsync(postgres);

        await host.SendAsync(new ApplyProductSnapshotCommand(1, Snapshots.Product()));

        var text = await host.QueryAsync(db => db.Products.AsNoTracking().Select(p => p.SearchText).SingleAsync());
        Assert.Equal("floral dress sty-1 girls dresses kids 4-8y summer red", text);
    }

    [Fact]
    [Trait("Spec", "SRC-8")]
    public async Task Apply_Rename_ChangesWhatIsFound()
    {
        using var host = await StorefrontHost.CreateAsync(postgres);
        await host.SendAsync(new ApplyProductSnapshotCommand(1, Snapshots.Product()));

        await host.SendAsync(new ApplyProductSnapshotCommand(1, Snapshots.Product(version: 2, name: "Dungaree set")));

        var found = await host.QueryAsync(async db => new
        {
            Old = await ScalarAsync<bool>(db, "SELECT EXISTS (SELECT 1 FROM \"STFR\".\"PRDT_MAIN\" WHERE \"SRCH_VEC\" @@ to_tsquery('simple', 'floral:*')) AS \"Value\""),
            New = await ScalarAsync<bool>(db, "SELECT EXISTS (SELECT 1 FROM \"STFR\".\"PRDT_MAIN\" WHERE \"SRCH_VEC\" @@ to_tsquery('simple', 'dungaree:*')) AS \"Value\""),
        });
        Assert.False(found.Old);
        Assert.True(found.New);
    }

    [Fact]
    [Trait("Spec", "SRC-8")]
    public async Task Vector_MatchesNameCategoryCollectionAndColourWords()
    {
        using var host = await StorefrontHost.CreateAsync(postgres);
        await host.SendAsync(new ApplyProductSnapshotCommand(1, Snapshots.Product()));

        foreach (var word in new[] { "floral", "dresses", "summer", "red", "girls", "wash" })
        {
            var hit = await host.QueryAsync(db => ScalarAsync<bool>(db,
                "SELECT EXISTS (SELECT 1 FROM \"STFR\".\"PRDT_MAIN\" WHERE \"SRCH_VEC\" @@ to_tsquery('simple', {0})) AS \"Value\"", word + ":*"));
            Assert.Equal(word != "wash", hit); // the description ("A dress.") is searched too, but the care note is not
        }
    }

    [Fact]
    [Trait("Spec", "SRC-8")]
    public async Task Trigram_ToleratesOneSlip()
    {
        using var host = await StorefrontHost.CreateAsync(postgres);
        await host.SendAsync(new ApplyProductSnapshotCommand(1, Snapshots.Product(name: "Dungaree set")));

        var hit = await host.QueryAsync(db => ScalarAsync<bool>(db,
            "SELECT EXISTS (SELECT 1 FROM \"STFR\".\"PRDT_MAIN\" WHERE word_similarity('dungree', \"SRCH_TEXT\") > 0.45) AS \"Value\""));
        Assert.True(hit);
    }

    [Fact]
    [Trait("Spec", "SRC-4")]
    public async Task Indexes_ExistForVectorTrigramAndSku()
    {
        using var host = await StorefrontHost.CreateAsync(postgres);

        var names = await host.QueryAsync(db => db.Database.SqlQueryRaw<string>(
            "SELECT indexname AS \"Value\" FROM pg_indexes WHERE schemaname = 'STFR'").ToListAsync());

        Assert.Contains("IX_PRDT_MAIN_SRCH_VEC", names);
        Assert.Contains("IX_PRDT_MAIN_SRCH_TEXT_TRGM", names);
        Assert.Contains("IX_PRDT_VRNT_SKU_NORM", names);
    }

    [Fact]
    [Trait("Spec", "SRC-12")]
    public async Task Migration_SeedsTheStarterSynonyms_WithEachTermInOneGroup()
    {
        using var host = await StorefrontHost.CreateAsync(postgres);

        var terms = await host.QueryAsync(db => db.SynonymTerms.AsNoTracking().ToListAsync());

        Assert.Equal(terms.Count, terms.Select(t => t.Term).Distinct().Count());
        foreach (var term in new[] { "frock", "dress", "kameez", "shalwar kameez", "salwar kameez", "dungaree", "pyjama", "t-shirt" })
        {
            Assert.Contains(terms, t => t.Term == term);
        }

        Assert.All(terms.Where(t => t.Term.Contains(' ')), t => Assert.True(t.IsPhrase));
        Assert.Equal(
            terms.Single(t => t.Term == "frock").GroupId,
            terms.Single(t => t.Term == "gown").GroupId);
    }
}
