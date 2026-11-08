using Microsoft.EntityFrameworkCore;
using Romp.Modules.Storefront.Application.ProductRecommendations;
using Romp.Modules.Storefront.Domain.ProductRecommendations;
using Romp.Modules.Storefront.Infrastructure.Persistence;

namespace Romp.Modules.Storefront.Infrastructure.ProductRecommendations;

/// <summary>
/// Adds to the day counters in one atomic statement (REC-12): insert the row for the day, or add to the one already there, so two clicks at once
/// give +2. Written only when the product is in the read model. Every identifier is quoted (docs/db/naming.md).
/// </summary>
internal sealed class RecommendationEventStore(StfrDbContext dbContext, TimeProvider clock) : IRecommendationEventStore
{
    public async Task<bool> AddAsync(RecommendationRowId row, long productId, DateOnly day, int clicks, int adds, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var rowId = (short)row;
        var written = await dbContext.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO "STFR"."RECM_EVNT_DAY" ("ROW_ID", "PRDT_ID", "EVNT_DTE", "CLK_CNT", "ADD_CNT", "INSR_DTE", "INSR_BY")
            SELECT {rowId}, {productId}, {day}, {clicks}, {adds}, {now}, 'public'
            WHERE EXISTS (SELECT 1 FROM "STFR"."PRDT_MAIN" WHERE "ID" = {productId})
            ON CONFLICT ("ROW_ID", "PRDT_ID", "EVNT_DTE") DO UPDATE SET
                "CLK_CNT" = "RECM_EVNT_DAY"."CLK_CNT" + EXCLUDED."CLK_CNT",
                "ADD_CNT" = "RECM_EVNT_DAY"."ADD_CNT" + EXCLUDED."ADD_CNT",
                "UPDT_DTE" = EXCLUDED."INSR_DTE",
                "UPDT_BY" = 'public'
            """,
            cancellationToken);
        return written > 0;
    }
}
