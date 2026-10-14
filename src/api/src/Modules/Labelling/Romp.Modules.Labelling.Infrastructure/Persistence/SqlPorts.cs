using Microsoft.EntityFrameworkCore;
using Romp.Modules.Labelling.Application.Abstractions;
using Romp.Modules.Labelling.Domain;

namespace Romp.Modules.Labelling.Infrastructure.Persistence;

/// <summary>
/// ADR 0009 / ADR 0006: the atomic GTIN counter. The upsert relies on PostgreSQL's row lock on the target row, so two concurrent
/// callers can never receive the same value; it runs inside the caller's transaction, so a rollback releases the number again
/// (there are no gaps) and a number that was handed out is never handed out twice. All identifiers are quoted.
/// </summary>
public sealed class GtinSequence(SkuDbContext dbContext) : IGtinSequence
{
    public async Task<long> NextAsync(string prefix, CancellationToken cancellationToken)
    {
        // ToListAsync, not SingleAsync: EF cannot compose SingleAsync over an INSERT...RETURNING statement.
        var rows = await dbContext.Database
            .SqlQueryRaw<long>(
                """
                INSERT INTO "SKU"."GTIN_NO_SEQ" ("PFX", "SEQ") VALUES ({0}, 1)
                ON CONFLICT ("PFX") DO UPDATE SET "SEQ" = "SKU"."GTIN_NO_SEQ"."SEQ" + 1
                RETURNING "SEQ";
                """,
                prefix)
            .ToListAsync(cancellationToken);
        return rows.Single();
    }

    public async Task<long> CurrentAsync(string prefix, CancellationToken cancellationToken)
    {
        var rows = await dbContext.Database
            .SqlQueryRaw<long>("""SELECT "SEQ" FROM "SKU"."GTIN_NO_SEQ" WHERE "PFX" = {0};""", prefix)
            .ToListAsync(cancellationToken);
        return rows.Count == 0 ? 0 : rows[0];
    }
}

/// <summary>ADR 0006: <c>PJ-{YYYY}-{NNNNN}</c> with the same atomic upsert, keyed by the Pakistan-time year.</summary>
public sealed class JobNumberAllocator(SkuDbContext dbContext, TimeProvider timeProvider) : IJobNumberAllocator
{
    public async Task<string> AllocateAsync(CancellationToken cancellationToken)
    {
        // Pakistan time is a fixed +05:00 (no daylight saving), like every staff-entered date.
        var year = (short)timeProvider.GetUtcNow().ToOffset(TimeSpan.FromHours(5)).Year;

        var rows = await dbContext.Database
            .SqlQueryRaw<int>(
                """
                INSERT INTO "SKU"."JOB_NO_SEQ" ("YR", "SEQ") VALUES ({0}, 1)
                ON CONFLICT ("YR") DO UPDATE SET "SEQ" = "SKU"."JOB_NO_SEQ"."SEQ" + 1
                RETURNING "SEQ";
                """,
                year)
            .ToListAsync(cancellationToken);

        return $"PJ-{year:D4}-{rows.Single():D5}";
    }
}

/// <summary>Row locks (<c>SELECT … FOR UPDATE</c>) that serialise check-then-act commands for one work item or variant.</summary>
public sealed class RowLocks(SkuDbContext dbContext) : IRowLocks
{
    public async Task LockWorkItemAsync(long workItemId, CancellationToken cancellationToken) =>
        _ = await dbContext.Database
            .SqlQueryRaw<long>("""SELECT "ID" FROM "SKU"."LBL_WORK_ITEM" WHERE "ID" = {0} FOR UPDATE;""", workItemId)
            .ToListAsync(cancellationToken);

    public async Task LockVariantAsync(long variantId, CancellationToken cancellationToken) =>
        _ = await dbContext.Database
            .SqlQueryRaw<long>("""SELECT "ID" FROM "SKU"."VRNT_MAIN" WHERE "ID" = {0} FOR UPDATE;""", variantId)
            .ToListAsync(cancellationToken);
}

/// <summary>
/// ADR 0010: the worker's atomic steps. The claim is ONE statement: <c>FOR UPDATE SKIP LOCKED</c> picks the oldest due job nobody else
/// holds and the UPDATE moves it to Printing and counts the attempt, so two workers can never print the same job (PJ-9).
/// </summary>
public sealed class PrintJobClaims(SkuDbContext dbContext) : IPrintJobClaims
{
    public async Task<long?> ClaimNextAsync(short printerId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var rows = await dbContext.Database
            .SqlQueryRaw<long>(
                """
                UPDATE "SKU"."PRT_JOB"
                   SET "PRT_JOB_STS_ID" = {0}, "ATMP_CNT" = "ATMP_CNT" + 1, "UPDT_DTE" = {2}, "UPDT_BY" = 'print-worker'
                 WHERE "ID" = (SELECT "ID" FROM "SKU"."PRT_JOB"
                                WHERE "PRT_JOB_STS_ID" = {1} AND "PRTR_ID" = {3} AND "NXT_ATMP_DTE" <= {2}
                                ORDER BY "NXT_ATMP_DTE", "ID"
                                FOR UPDATE SKIP LOCKED
                                LIMIT 1)
                RETURNING "ID";
                """,
                PrintJobStatus.Printing, PrintJobStatus.Queued, now, printerId)
            .ToListAsync(cancellationToken);
        return rows.Count == 0 ? null : rows[0];
    }

    public async Task<int> RecoverStuckAsync(DateTimeOffset now, TimeSpan olderThan, CancellationToken cancellationToken) =>
        await dbContext.Database.ExecuteSqlRawAsync(
            """
            UPDATE "SKU"."PRT_JOB"
               SET "PRT_JOB_STS_ID" = {0}, "NXT_ATMP_DTE" = {2}, "UPDT_DTE" = {2}, "UPDT_BY" = 'print-worker'
             WHERE "PRT_JOB_STS_ID" = {1} AND "UPDT_DTE" < {3};
            """,
            [PrintJobStatus.Queued, PrintJobStatus.Printing, now, now - olderThan],
            cancellationToken);
}
