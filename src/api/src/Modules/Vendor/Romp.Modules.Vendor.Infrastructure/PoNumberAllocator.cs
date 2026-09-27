using Microsoft.EntityFrameworkCore;
using Romp.Modules.Vendor.Application;

namespace Romp.Modules.Vendor.Infrastructure;

/// <summary>
/// ADR 0006: atomically allocates the next <c>PO-{YYYY}-{NNNNN}</c> number. The
/// <c>INSERT ... ON CONFLICT ("YR") DO UPDATE ... RETURNING "SEQ"</c> upsert relies on PostgreSQL's
/// row-level locking on the upsert target row, so two concurrent callers can never read and return
/// the same SEQ value (AC-7b) - safe without an app-level lock or SERIALIZABLE isolation.
/// </summary>
public sealed class PoNumberAllocator(VendorDbContext dbContext, TimeProvider timeProvider) : IPoNumberAllocator
{
    public async Task<string> AllocateAsync(CancellationToken cancellationToken)
    {
        var year = (short)timeProvider.GetUtcNow().Year;

        var sequence = await dbContext.Database
            .SqlQueryRaw<int>(
                """
                INSERT INTO "VNDR"."PO_NO_SEQ" ("YR", "SEQ") VALUES ({0}, 1)
                ON CONFLICT ("YR") DO UPDATE SET "SEQ" = "VNDR"."PO_NO_SEQ"."SEQ" + 1
                RETURNING "SEQ";
                """,
                year)
            .SingleAsync(cancellationToken);

        return $"PO-{year:D4}-{sequence:D5}";
    }
}
