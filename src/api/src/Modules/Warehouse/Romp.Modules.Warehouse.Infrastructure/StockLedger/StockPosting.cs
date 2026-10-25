using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Domain;
using Romp.BuildingBlocks.Persistence.Auditing;
using Romp.Modules.Warehouse.Application.StockLedger;
using Romp.Modules.Warehouse.Domain;
using Romp.Modules.Warehouse.Domain.StockLedger;
using Romp.Modules.Warehouse.Infrastructure.Persistence;

namespace Romp.Modules.Warehouse.Infrastructure.StockLedger;

/// <summary>The balance of one bin row before and after a statement (the columns the posting SQL returns).</summary>
public sealed class BalanceChange
{
    public int Bef { get; set; }

    public int Aft { get; set; }
}

/// <summary>
/// ADR 0012: the single writer of bin balances and the ledger. Each change is one atomic SQL statement (an upsert for a positive
/// change, a conditional update for a negative one), so concurrent changes to one bin serialise on its row and a balance can never go
/// below zero (the CHECK constraint is the last line of defence). It runs inside the caller's transaction: if anything later fails,
/// balances, ledger rows and events all roll back together. Every identifier is quoted.
/// </summary>
public sealed class StockPosting(WhseDbContext dbContext, TimeProvider timeProvider, ICurrentActor currentActor) : IStockPosting
{
    private const string PositiveSql =
        """
        INSERT INTO "WHSE"."STCK_BAL" ("VRNT_REF_ID", "GRD_ID", "BIN_ID", "QTY", "INSR_DTE", "INSR_BY")
        VALUES ({0}, {1}, {2}, {3}, {4}, {5})
        ON CONFLICT ("VRNT_REF_ID", "GRD_ID", "BIN_ID") DO UPDATE
            SET "QTY" = "STCK_BAL"."QTY" + {3}, "UPDT_DTE" = {4}, "UPDT_BY" = {5}
        RETURNING "QTY" - {3} AS "Bef", "QTY" AS "Aft";
        """;

    private const string NegativeSql =
        """
        UPDATE "WHSE"."STCK_BAL"
        SET "QTY" = "QTY" + {3}, "UPDT_DTE" = {4}, "UPDT_BY" = {5}
        WHERE "VRNT_REF_ID" = {0} AND "GRD_ID" = {1} AND "BIN_ID" = {2} AND "QTY" + {3} >= 0
        RETURNING "QTY" - {3} AS "Bef", "QTY" AS "Aft";
        """;

    // PCT-5: one increment per variant per posting. The UPDATE takes the variant row's lock until the transaction ends, so postings of
    // one variant are serialised and their versions are in commit order (a ledger row id is not: ids are handed out at insert time).
    private const string VersionSql =
        """
        UPDATE "WHSE"."VRNT_REF"
        SET "STCK_VER" = "STCK_VER" + 1, "UPDT_DTE" = {1}, "UPDT_BY" = {2}
        WHERE "ID" = {0}
        RETURNING "STCK_VER" AS "Value";
        """;

    public async Task<IReadOnlyList<PostedMovement>> PostAsync(IReadOnlyList<StockPost> posts, string actingPerson, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(posts);
        if (posts.Count is 0 or > 500)
        {
            throw new DomainException("A stock posting needs between 1 and 500 changes.");
        }

        var now = timeProvider.GetUtcNow();
        var by = currentActor.UserName;
        var posted = new List<PostedMovement>(posts.Count);

        // Variants first, in id order, then bins: every posting takes its locks in the same order, so none can deadlock another.
        var versions = new Dictionary<long, long>();
        foreach (var variantId in posts.Select(p => p.VariantRefId).Distinct().Order())
        {
            var next = await dbContext.Database.SqlQueryRaw<long>(VersionSql, variantId, now, by).ToListAsync(cancellationToken);
            versions[variantId] = next.Count == 1 ? next[0] : throw new DomainException($"Variant {variantId} is not known to the warehouse.");
        }

        // A fixed order means two postings that touch the same bins always lock rows in the same order (no deadlock).
        foreach (var post in posts.OrderBy(p => p.VariantRefId).ThenBy(p => p.GradeId).ThenBy(p => p.BinId))
        {
            var rows = await dbContext.Database
                .SqlQueryRaw<BalanceChange>(
                    post.QuantityChange > 0 ? PositiveSql : NegativeSql,
                    post.VariantRefId, post.GradeId, post.BinId, post.QuantityChange, now, by)
                .ToListAsync(cancellationToken);

            if (rows.Count == 0)
            {
                throw new DomainException(StockMessages.Insufficient);
            }

            var change = rows[0];
            dbContext.Movements.Add(new StockMovement(
                post.VariantRefId, post.GradeId, post.BinId, post.KindId, post.SourceId, post.QuantityChange, change.Bef, change.Aft, actingPerson));
            posted.Add(new PostedMovement(post, change.Bef, change.Aft, versions[post.VariantRefId]));
        }

        foreach (var group in posts.GroupBy(p => p.VariantRefId))
        {
            var totals = await dbContext.Balances.AsNoTracking()
                .Where(b => b.VariantRefId == group.Key)
                .GroupBy(b => b.GradeId)
                .Select(g => new { Grade = g.Key, Total = g.Sum(b => b.Quantity) })
                .ToListAsync(cancellationToken);

            var variant = await dbContext.VariantRefs.FirstAsync(v => v.Id == group.Key, cancellationToken);
            variant.PublishLevel(
                totals.Where(t => t.Grade == Grades.Accepted).Sum(t => t.Total),
                totals.Where(t => t.Grade == Grades.Concession).Sum(t => t.Total),
                MovementKinds.CodeOf(group.First().KindId),
                versions[group.Key]);
        }

        return posted;
    }
}
