using Microsoft.EntityFrameworkCore;
using Romp.Modules.Quality.Application.Abstractions;
using Romp.Modules.Quality.Infrastructure.Persistence;

namespace Romp.Modules.Quality.Infrastructure.Receiving;

/// <summary>
/// ADR 0006: atomically allocates the next <c>GRN-{YYYY}-{NNNNN}</c> number. The
/// <c>INSERT ... ON CONFLICT ("YR") DO UPDATE ... RETURNING "SEQ"</c> upsert relies on PostgreSQL's row-level locking on
/// the upsert target row, so two concurrent callers can never receive the same SEQ (AC-3). It runs inside the caller's
/// transaction, so a rolled-back receipt only leaves a gap, never a duplicate.
/// </summary>
public sealed class GrnNumberAllocator(QcDbContext dbContext, TimeProvider timeProvider) : IGrnNumberAllocator
{
    public async Task<string> AllocateAsync(CancellationToken cancellationToken)
    {
        // The year is the Pakistan-time year, like every staff-entered date (fixed +05:00, no daylight saving).
        var year = (short)PakistanClock.Today(timeProvider).Year;

        // ToListAsync, not SingleAsync: EF cannot compose SingleAsync over an INSERT...RETURNING statement.
        var rows = await dbContext.Database
            .SqlQueryRaw<int>(
                """
                INSERT INTO "QC"."GRN_NO_SEQ" ("YR", "SEQ") VALUES ({0}, 1)
                ON CONFLICT ("YR") DO UPDATE SET "SEQ" = "QC"."GRN_NO_SEQ"."SEQ" + 1
                RETURNING "SEQ";
                """,
                year)
            .ToListAsync(cancellationToken);

        return $"GRN-{year:D4}-{rows.Single():D5}";
    }
}
