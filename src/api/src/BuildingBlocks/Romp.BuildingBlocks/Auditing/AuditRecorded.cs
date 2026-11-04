using System.Text.Json.Serialization;
using Romp.BuildingBlocks.Domain;

namespace Romp.BuildingBlocks.Auditing;

/// <summary>One changed record: which table, which row, what happened, and the NAMES of the columns that changed. Never a value (audit-trail AC-6).</summary>
public sealed record AuditRecord(string EntityType, string EntityKey, string Operation, IReadOnlyList<string> Columns);

/// <summary>
/// The integration event every module writes to its own outbox, in the same transaction as the change it describes (ADR 0016). At most
/// <see cref="MaxRecords"/> records per event. Identity is the only consumer. The payload holds names and keys only, so no personal or
/// child data content can reach the audit store.
/// </summary>
public sealed record AuditRecorded(
    Guid EventId,
    DateTimeOffset OccurredAt,
    string Module,
    string Actor,
    string? CorrelationId,
    IReadOnlyList<AuditRecord> Records) : IOutboxEvent
{
    public const int MaxRecords = 500;

    public const string Insert = "INSERT";
    public const string Update = "UPDATE";
    public const string Delete = "DELETE";

    [JsonIgnore]
    public string AggregateType => "Audit";

    [JsonIgnore]
    public long AggregateId => 0;

    [JsonIgnore]
    public short SchemaVersion => 1;
}
