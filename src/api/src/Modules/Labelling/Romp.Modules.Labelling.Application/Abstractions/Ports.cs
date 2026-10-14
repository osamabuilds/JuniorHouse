namespace Romp.Modules.Labelling.Application.Abstractions;

/// <summary>The atomic GTIN counter (ADR 0009). Implemented in Infrastructure with the ADR 0006 upsert, inside the caller's transaction.</summary>
public interface IGtinSequence
{
    /// <summary>Takes the next item reference for the prefix. A rolled-back transaction releases it again; it is never handed out twice.</summary>
    Task<long> NextAsync(string prefix, CancellationToken cancellationToken);

    /// <summary>The highest item reference handed out so far (0 when none).</summary>
    Task<long> CurrentAsync(string prefix, CancellationToken cancellationToken);
}

/// <summary>Allocates <c>PJ-{YYYY}-{NNNNN}</c> job numbers (ADR 0006).</summary>
public interface IJobNumberAllocator
{
    Task<string> AllocateAsync(CancellationToken cancellationToken);
}

/// <summary>Row locks that serialise commands which would otherwise check-then-act (concurrency, ADR 0011).</summary>
public interface IRowLocks
{
    Task LockWorkItemAsync(long workItemId, CancellationToken cancellationToken);

    Task LockVariantAsync(long variantId, CancellationToken cancellationToken);
}

/// <summary>The worker's database steps that must be single atomic statements (ADR 0010).</summary>
public interface IPrintJobClaims
{
    /// <summary>
    /// Claims the next due Queued job of a printer: one conditional UPDATE using <c>FOR UPDATE SKIP LOCKED</c> that moves it to
    /// Printing and counts the attempt. Two workers can never claim the same job. Returns its id, or null when none is due.
    /// </summary>
    Task<long?> ClaimNextAsync(short printerId, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>Returns jobs stuck in Printing for longer than <paramref name="olderThan"/> to Queued (a crashed worker). Returns how many.</summary>
    Task<int> RecoverStuckAsync(DateTimeOffset now, TimeSpan olderThan, CancellationToken cancellationToken);
}
