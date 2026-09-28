namespace Romp.BuildingBlocks.Persistence;

/// <summary>
/// The single, platform-owned outbox dispatcher (SCRUM-181; ADR 0007 refines ADR 0004) - not owned
/// by any one module. Iterates every module registered via
/// <see cref="OutboxServiceCollectionExtensions.AddOutboxModule"/>, one schema-qualified query per
/// module, never a cross-schema union (ADR 0002).
/// </summary>
public interface IOutboxDispatcher
{
    /// <summary>One claim-and-dispatch pass across every registered schema. Returns how many messages were delivered (marked Processed) this pass - tests call this directly instead of waiting on the hosted service's timer.</summary>
    Task<int> DispatchOnceAsync(CancellationToken cancellationToken);
}
