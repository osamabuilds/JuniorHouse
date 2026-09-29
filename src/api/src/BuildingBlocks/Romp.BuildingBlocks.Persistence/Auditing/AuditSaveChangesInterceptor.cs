using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Romp.BuildingBlocks.Domain;

namespace Romp.BuildingBlocks.Persistence.Auditing;

/// <summary>
/// Stamps the audit columns every transactional table has (docs/db/naming.md section 2) on every
/// <c>SaveChanges</c> call: INSR_DTE/INSR_BY on insert, UPDT_DTE/UPDT_BY on update. Optimistic
/// concurrency uses PostgreSQL's <c>xmin</c> system column instead (see
/// <see cref="AuditableEntityTypeBuilderExtensions"/>), so there's no app-set version column for
/// this interceptor to maintain. One instance is shared by every module's DbContext.
/// </summary>
public sealed class AuditSaveChangesInterceptor(TimeProvider timeProvider, ICurrentActor currentActor)
    : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Stamp(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Stamp(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Stamp(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var now = timeProvider.GetUtcNow();

        // Forces EF to notice entities reachable only through a navigation on an already-tracked
        // parent (e.g. a new PoStatusHistoryEntry added to a loaded PurchaseOrder's collection)
        // before we read ChangeTracker.Entries below - without this, a same-tick SavingChanges can
        // still see it as Unchanged/undetected and skip stamping it.
        context.ChangeTracker.DetectChanges();

        foreach (var entry in context.ChangeTracker.Entries<IAuditable>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.InsrDte = now;
                    entry.Entity.InsrBy = currentActor.UserName;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdtDte = now;
                    entry.Entity.UpdtBy = currentActor.UserName;
                    break;
            }
        }
    }
}
