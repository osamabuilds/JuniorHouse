using System.Text.RegularExpressions;
using Romp.BuildingBlocks.Auditing;

namespace Romp.Modules.Identity.Domain.AuditTrail;

/// <summary>What happened to a record. The values are the ids of <c>AUDT_OPRN_LKP</c> and are never reused.</summary>
public enum AuditOperation : short
{
    Insert = 1,
    Update = 2,
    Delete = 3,
}

/// <summary>
/// IDNT.AUDT_ENTY: one immutable row per changed record, from any module (ADR 0016). It holds the record table and key, the
/// operation, who did it and when, and (in <see cref="Columns"/>) the NAMES of the changed columns, never a value. Append-only: the
/// database rejects an update or delete.
/// </summary>
[NotAudited("This is the audit store itself; it is append-only and never re-audited.")]
public sealed partial class AuditEntry
{
    public const int ModuleMaxLength = 20;
    public const int EntityTypeMaxLength = 100;
    public const int EntityKeyMaxLength = 200;
    public const int ActorMaxLength = 100;
    public const int CorrelationMaxLength = 64;
    public const int ColumnMaxLength = 100;

    public const string UnknownActor = "unknown";

    private AuditEntry()
    {
        Module = string.Empty;
        EntityType = string.Empty;
        EntityKey = string.Empty;
        Actor = string.Empty;
    }

    public long Id { get; private set; }

    /// <summary>When the change happened (the producer save time), not when the entry was stored; also the partition key.</summary>
    public DateTimeOffset OccurredAt { get; private set; }

    /// <summary>The producing module schema code, for example <c>WHSE</c>.</summary>
    public string Module { get; private set; }

    /// <summary>The producing module outbox message, so a redelivery is recognised.</summary>
    public long SourceMessageId { get; private set; }

    /// <summary>The table the record lives in, for example <c>STYL_MAIN</c>.</summary>
    public string EntityType { get; private set; }

    /// <summary>The record key; a composite key is joined with <c>|</c>.</summary>
    public string EntityKey { get; private set; }

    public AuditOperation Operation { get; private set; }

    public string Actor { get; private set; }

    public string? CorrelationId { get; private set; }

    public List<AuditColumn> Columns { get; private set; } = [];

    /// <summary>
    /// Builds an entry from a delivered record. It came through a message, so everything is checked; an actor name that does not fit the
    /// actor rule is stored as <c>unknown</c> rather than refused, so the entry is never lost.
    /// </summary>
    public static AuditEntry Create(
        DateTimeOffset occurredAt,
        string module,
        long sourceMessageId,
        string entityType,
        string entityKey,
        AuditOperation operation,
        string actor,
        string? correlationId,
        IEnumerable<string> columns)
    {
        if (!Enum.IsDefined(operation))
        {
            throw new ArgumentOutOfRangeException(nameof(operation));
        }

        var entry = new AuditEntry
        {
            OccurredAt = occurredAt.ToUniversalTime(),
            Module = Bounded(module, ModuleMaxLength, nameof(module)),
            SourceMessageId = sourceMessageId,
            EntityType = Bounded(entityType, EntityTypeMaxLength, nameof(entityType)),
            EntityKey = Bounded(entityKey, EntityKeyMaxLength, nameof(entityKey)),
            Operation = operation,
            Actor = IsActorName(actor) ? actor : UnknownActor,
            CorrelationId = IsCorrelation(correlationId) ? correlationId : null,
        };

        foreach (var column in columns.Distinct(StringComparer.Ordinal))
        {
            if (!IsColumnName(column))
            {
                throw new ArgumentException("A column name must be upper-case letters, digits and underscores.", nameof(columns));
            }

            entry.Columns.Add(new AuditColumn(column));
        }

        return entry;
    }

    public static bool IsActorName(string? name) =>
        !string.IsNullOrEmpty(name) && name.Length <= ActorMaxLength && ActorPattern().IsMatch(name);

    private static bool IsCorrelation(string? value) =>
        !string.IsNullOrEmpty(value) && value.Length <= CorrelationMaxLength && CorrelationPattern().IsMatch(value);

    private static bool IsColumnName(string value) =>
        value.Length is > 0 and <= ColumnMaxLength && ColumnPattern().IsMatch(value);

    private static string Bounded(string value, int max, string name)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > max || value.Any(char.IsControl))
        {
            throw new ArgumentException($"{name} must be 1 to {max} characters without control characters.", name);
        }

        return value;
    }

    [GeneratedRegex("^[a-z0-9._:-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex ActorPattern();

    [GeneratedRegex("^[A-Za-z0-9-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex CorrelationPattern();

    [GeneratedRegex("^[A-Z0-9_]+$", RegexOptions.CultureInvariant)]
    private static partial Regex ColumnPattern();
}

/// <summary>IDNT.AUDT_CHNG: the name of one column that changed in an <see cref="AuditEntry"/>.</summary>
[NotAudited("This is the audit store itself; it is append-only and never re-audited.")]
public sealed class AuditColumn
{
    private AuditColumn()
    {
        Name = string.Empty;
    }

    internal AuditColumn(string name)
    {
        Name = name;
    }

    public long Id { get; private set; }

    public long EntryId { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public string Name { get; private set; }
}
