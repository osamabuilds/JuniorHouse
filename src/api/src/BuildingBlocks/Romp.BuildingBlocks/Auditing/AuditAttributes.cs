namespace Romp.BuildingBlocks.Auditing;

/// <summary>
/// Keeps a whole entity type out of the audit trail: technical rows (outbox, inbox, sessions, pending sign-ins) that change without
/// anyone deciding anything. Every use needs a reason, so a reviewer can see why a table is invisible.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class NotAuditedAttribute(string reason) : Attribute
{
    public string Reason { get; } = reason;
}

/// <summary>Keeps a column's name out of the audit trail (secrets: hashes, ciphertext, token digests). Such columns are also excluded by name pattern, so forgetting the attribute is not enough to leak one.</summary>
[AttributeUsage(AttributeTargets.Property, Inherited = false)]
public sealed class AuditExcludedAttribute : Attribute;
