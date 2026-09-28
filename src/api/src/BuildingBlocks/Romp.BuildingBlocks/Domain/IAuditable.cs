namespace Romp.BuildingBlocks.Domain;

/// <summary>
/// Marks an entity as carrying the audit columns every transactional table has
/// (docs/db/naming.md section 2: INSR_DTE/INSR_BY, UPDT_DTE/UPDT_BY). The
/// AuditSaveChangesInterceptor (Romp.BuildingBlocks.Persistence) stamps these automatically on
/// insert/update. Lookup tables don't implement this - they're seeded by migration, not
/// created/updated by application code.
/// </summary>
public interface IAuditable
{
    DateTimeOffset InsrDte { get; set; }

    string InsrBy { get; set; }

    DateTimeOffset? UpdtDte { get; set; }

    string? UpdtBy { get; set; }
}
