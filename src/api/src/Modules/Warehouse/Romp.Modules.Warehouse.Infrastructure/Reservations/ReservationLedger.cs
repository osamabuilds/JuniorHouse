using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Domain;
using Romp.BuildingBlocks.Persistence.Auditing;
using Romp.Modules.Warehouse.Application.Reservations;
using Romp.Modules.Warehouse.Domain;
using Romp.Modules.Warehouse.Infrastructure.Persistence;

namespace Romp.Modules.Warehouse.Infrastructure.Reservations;

/// <summary>
/// ADR 0025: the single writer of reserved quantities. Reserving a variant is one conditional statement (insert, or increase only where
/// on-hand minus reserved still covers the quantity), so concurrent reservations serialise on the variant row and the guard belongs to the
/// database, never to application code. It runs inside the caller transaction; lines go in ascending variant order. Every identifier is quoted.
/// </summary>
public sealed class ReservationLedger(WhseDbContext dbContext, TimeProvider timeProvider, ICurrentActor currentActor) : IReservationLedger
{
    private const string ReserveSql =
        """
        INSERT INTO "WHSE"."STCK_RSRV_BAL" ("VRNT_REF_ID", "RSRV_QTY", "INSR_DTE", "INSR_BY")
        SELECT {0}, {1}, {2}, {3}
        WHERE (SELECT COALESCE(SUM("QTY"), 0) FROM "WHSE"."STCK_BAL" WHERE "VRNT_REF_ID" = {0}) >= {1}
        ON CONFLICT ("VRNT_REF_ID") DO UPDATE
            SET "RSRV_QTY" = "STCK_RSRV_BAL"."RSRV_QTY" + {1}, "UPDT_DTE" = {2}, "UPDT_BY" = {3}
            WHERE (SELECT COALESCE(SUM("QTY"), 0) FROM "WHSE"."STCK_BAL" WHERE "VRNT_REF_ID" = {0}) - "STCK_RSRV_BAL"."RSRV_QTY" >= {1}
        RETURNING "RSRV_QTY" AS "Value";
        """;

    private const string LockSql = """SELECT "ID" FROM "WHSE"."VRNT_REF" WHERE "ID" = {0} FOR UPDATE""";

    private const string ReleaseSql =
        """
        UPDATE "WHSE"."STCK_RSRV_BAL"
        SET "RSRV_QTY" = "RSRV_QTY" - {1}, "UPDT_DTE" = {2}, "UPDT_BY" = {3}
        WHERE "VRNT_REF_ID" = {0} AND "RSRV_QTY" >= {1}
        RETURNING "RSRV_QTY" AS "Value";
        """;

    // Same version rule as the posting port (PCT-5): one increment per variant per change, taken under the variant row lock.
    private const string VersionSql =
        """
        UPDATE "WHSE"."VRNT_REF"
        SET "STCK_VER" = "STCK_VER" + 1, "UPDT_DTE" = {1}, "UPDT_BY" = {2}
        WHERE "ID" = {0}
        RETURNING "STCK_VER" AS "Value";
        """;

    public async Task<IReadOnlyList<ReservationShortage>> TryReserveAllAsync(IReadOnlyList<ReservedLine> lines, string reasonCode, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lines);
        var transaction = dbContext.Database.CurrentTransaction ?? throw new InvalidOperationException("Reserving stock must run inside a transaction.");
        var now = timeProvider.GetUtcNow();
        var by = currentActor.UserName;

        // The variant row lock comes first, like in the posting port, so a reservation and a stock change of one variant take turns (RSV-8).
        foreach (var line in lines.OrderBy(l => l.VariantRefId))
        {
            await dbContext.Database.ExecuteSqlRawAsync(LockSql, [line.VariantRefId], cancellationToken);
        }

        await transaction.CreateSavepointAsync("reserve", cancellationToken);
        var failed = false;
        foreach (var line in lines.OrderBy(l => l.VariantRefId))
        {
            var rows = await dbContext.Database.SqlQueryRaw<int>(ReserveSql, line.VariantRefId, line.Quantity, now, by).ToListAsync(cancellationToken);
            if (rows.Count == 0)
            {
                failed = true;
                break;
            }
        }

        if (failed)
        {
            // Everything reserved by this call is undone, then every line is measured so the caller sees all the shortfalls at once (RSV-2).
            await transaction.RollbackToSavepointAsync("reserve", cancellationToken);
            var available = await AvailableAsync([.. lines.Select(l => l.VariantRefId)], cancellationToken);
            var shortages = lines.Where(l => available.GetValueOrDefault(l.VariantRefId) < l.Quantity)
                .Select(l => new ReservationShortage(l.VariantRefId, l.Quantity, Math.Max(0, available.GetValueOrDefault(l.VariantRefId))))
                .ToList();
            return shortages.Count > 0 ? shortages : [new ReservationShortage(lines[0].VariantRefId, lines[0].Quantity, 0)];
        }

        await transaction.ReleaseSavepointAsync("reserve", cancellationToken);
        await PublishLevelsAsync([.. lines.Select(l => l.VariantRefId)], reasonCode, now, by, cancellationToken);
        return [];
    }

    public async Task ReleaseAsync(IReadOnlyList<ReservedLine> lines, string reasonCode, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lines);
        var now = timeProvider.GetUtcNow();
        var by = currentActor.UserName;
        foreach (var line in lines.OrderBy(l => l.VariantRefId))
        {
            var rows = await dbContext.Database.SqlQueryRaw<int>(ReleaseSql, line.VariantRefId, line.Quantity, now, by).ToListAsync(cancellationToken);
            if (rows.Count == 0)
            {
                throw new DomainException($"Variant {line.VariantRefId} has fewer units reserved than this reservation holds.");
            }
        }

        await PublishLevelsAsync([.. lines.Select(l => l.VariantRefId)], reasonCode, now, by, cancellationToken);
    }

    /// <summary>On-hand minus reserved for each variant, in two grouped queries.</summary>
    private async Task<Dictionary<long, int>> AvailableAsync(IReadOnlyCollection<long> variantRefIds, CancellationToken cancellationToken)
    {
        var onHand = await dbContext.Balances.AsNoTracking().Where(b => variantRefIds.Contains(b.VariantRefId))
            .GroupBy(b => b.VariantRefId).Select(g => new { Id = g.Key, Total = g.Sum(b => b.Quantity) }).ToDictionaryAsync(x => x.Id, x => x.Total, cancellationToken);
        var reserved = await dbContext.ReservedBalances.AsNoTracking().Where(b => variantRefIds.Contains(b.Id))
            .ToDictionaryAsync(b => b.Id, b => b.Quantity, cancellationToken);
        return variantRefIds.ToDictionary(id => id, id => onHand.GetValueOrDefault(id) - reserved.GetValueOrDefault(id));
    }

    private async Task PublishLevelsAsync(IReadOnlyList<long> variantRefIds, string reasonCode, DateTimeOffset now, string by, CancellationToken cancellationToken)
    {
        foreach (var id in variantRefIds.Distinct().Order())
        {
            var versions = await dbContext.Database.SqlQueryRaw<long>(VersionSql, id, now, by).ToListAsync(cancellationToken);
            var version = versions.Count == 1 ? versions[0] : throw new DomainException($"Variant {id} is not known to the warehouse.");

            var totals = await dbContext.Balances.AsNoTracking().Where(b => b.VariantRefId == id)
                .GroupBy(b => b.GradeId).Select(g => new { Grade = g.Key, Total = g.Sum(b => b.Quantity) }).ToListAsync(cancellationToken);
            var reserved = await dbContext.ReservedBalances.AsNoTracking().Where(b => b.Id == id).Select(b => b.Quantity).FirstOrDefaultAsync(cancellationToken);
            var variant = await dbContext.VariantRefs.FirstAsync(v => v.Id == id, cancellationToken);
            variant.PublishLevel(
                totals.Where(t => t.Grade == Grades.Accepted).Sum(t => t.Total),
                totals.Where(t => t.Grade == Grades.Concession).Sum(t => t.Total),
                reasonCode,
                version,
                reserved);
        }
    }
}
