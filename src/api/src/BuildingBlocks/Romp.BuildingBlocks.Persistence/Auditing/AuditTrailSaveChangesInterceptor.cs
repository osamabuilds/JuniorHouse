using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using NpgsqlTypes;
using Romp.BuildingBlocks.Auditing;

namespace Romp.BuildingBlocks.Persistence.Auditing;

/// <summary>
/// The audit trail's collector (ADR 0016, audit-trail AC-2, AC-3, AC-6, AC-10). During <c>SaveChanges</c> it notes one record per added,
/// modified or deleted entity: the table, the row key, the operation and the NAMES of the changed columns, never a value. Right after
/// the save succeeds (keys of new rows now exist) it writes the records as <see cref="AuditRecorded"/> messages into the module's own
/// outbox, inside the same database transaction, so a rolled-back change leaves no trace and a committed one always has its trail
/// (a stopped dispatcher loses nothing: the messages wait in the outbox). When the caller has no transaction open, the interceptor
/// opens one around the save and commits it itself.
/// Technical tables (<see cref="NotAuditedAttribute"/>) and secret columns (<see cref="AuditExcludedAttribute"/>, or a name that says
/// hash, secret, token or pepper) never appear.
/// </summary>
public sealed class AuditTrailSaveChangesInterceptor(TimeProvider timeProvider, ICurrentActor currentActor) : SaveChangesInterceptor
{
    private static readonly string[] SecretWords = ["HASH", "SECR", "TOKN", "PEPR", "CIPH"];

    private static readonly HashSet<string> StampColumns = ["INSR_DTE", "INSR_BY", "UPDT_DTE", "UPDT_BY"];

    private readonly ConditionalWeakTable<DbContext, Pending> _pending = new();

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (Collect(eventData.Context) is { } pending && pending.NeedsOwnTransaction)
        {
            pending.OwnTransaction = eventData.Context!.Database.BeginTransaction();
        }

        return base.SavingChanges(eventData, result);
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (Collect(eventData.Context) is { } pending && pending.NeedsOwnTransaction)
        {
            pending.OwnTransaction = await eventData.Context!.Database.BeginTransactionAsync(cancellationToken);
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        if (Take(eventData.Context) is { } pending)
        {
            try
            {
                foreach (var command in BuildCommands(eventData.Context!, pending))
                {
                    command.ExecuteNonQuery();
                    command.Dispose();
                }

                pending.OwnTransaction?.Commit();
            }
            finally
            {
                pending.OwnTransaction?.Dispose();
            }
        }

        return base.SavedChanges(eventData, result);
    }

    public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        if (Take(eventData.Context) is { } pending)
        {
            try
            {
                foreach (var command in BuildCommands(eventData.Context!, pending))
                {
                    await using (command)
                    {
                        await command.ExecuteNonQueryAsync(cancellationToken);
                    }
                }

                if (pending.OwnTransaction is not null)
                {
                    await pending.OwnTransaction.CommitAsync(cancellationToken);
                }
            }
            finally
            {
                if (pending.OwnTransaction is not null)
                {
                    await pending.OwnTransaction.DisposeAsync();
                }
            }
        }

        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        Take(eventData.Context)?.OwnTransaction?.Dispose();
        base.SaveChangesFailed(eventData);
    }

    public override async Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        if (Take(eventData.Context)?.OwnTransaction is { } transaction)
        {
            await transaction.DisposeAsync();
        }

        await base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    public override void SaveChangesCanceled(DbContextEventData eventData)
    {
        Take(eventData.Context)?.OwnTransaction?.Dispose();
        base.SaveChangesCanceled(eventData);
    }

    public override async Task SaveChangesCanceledAsync(DbContextEventData eventData, CancellationToken cancellationToken = default)
    {
        if (Take(eventData.Context)?.OwnTransaction is { } transaction)
        {
            await transaction.DisposeAsync();
        }

        await base.SaveChangesCanceledAsync(eventData, cancellationToken);
    }

    private Pending? Take(DbContext? context)
    {
        if (context is null || !_pending.TryGetValue(context, out var pending))
        {
            return null;
        }

        _pending.Remove(context);
        return pending;
    }

    private Pending? Collect(DbContext? context)
    {
        if (context is null)
        {
            return null;
        }

        _pending.Remove(context);
        context.ChangeTracker.DetectChanges();

        var notes = new List<Note>();
        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
            {
                continue;
            }

            var entityType = entry.Metadata;
            var table = entityType.GetTableName();
            if (table is null || IsNotAudited(entityType.ClrType) || entityType.FindPrimaryKey() is not { } key)
            {
                continue;
            }

            var columns = entry.State == EntityState.Modified ? ChangedColumns(entry) : AllColumns(entry);
            var operation = entry.State switch
            {
                EntityState.Added => AuditRecorded.Insert,
                EntityState.Modified => AuditRecorded.Update,
                _ => AuditRecorded.Delete,
            };

            // New rows get their generated key during the save, so it is read afterwards from the same entry.
            notes.Add(new Note(table, operation, columns, entry, key, entry.State == EntityState.Added ? null : KeyOf(entry, key)));
        }

        if (notes.Count == 0)
        {
            return null;
        }

        var pending = new Pending(notes, currentActor.UserName, Activity.Current?.TraceId.ToString(), timeProvider.GetUtcNow())
        {
            NeedsOwnTransaction = context.Database.CurrentTransaction is null,
        };
        _pending.Add(context, pending);
        return pending;
    }

    private static bool IsNotAudited(Type clrType) => clrType.GetCustomAttribute<NotAuditedAttribute>() is not null;

    private static List<string> ChangedColumns(EntityEntry entry)
    {
        var all = entry.Properties
            .Where(p => p.IsModified && !p.Metadata.IsPrimaryKey() && !p.Metadata.IsConcurrencyToken)
            .Select(p => (Property: p.Metadata, Column: p.Metadata.GetColumnName()))
            .Where(p => !IsSecret(p.Property, p.Column))
            .Select(p => p.Column)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

        // The stamping columns change on every update; they say nothing about what was edited unless they are all there is.
        var business = all.Where(c => !StampColumns.Contains(c)).ToList();
        return business.Count > 0 ? business : [];
    }

    private static List<string> AllColumns(EntityEntry entry) => entry.Properties
        .Where(p => !p.Metadata.IsConcurrencyToken)
        .Select(p => (Property: p.Metadata, Column: p.Metadata.GetColumnName()))
        .Where(p => !IsSecret(p.Property, p.Column) && !StampColumns.Contains(p.Column))
        .Select(p => p.Column)
        .Distinct(StringComparer.Ordinal)
        .Order(StringComparer.Ordinal)
        .ToList();

    private static bool IsSecret(IProperty property, string column) =>
        property.PropertyInfo?.GetCustomAttribute<AuditExcludedAttribute>() is not null
        || property.FieldInfo?.GetCustomAttribute<AuditExcludedAttribute>() is not null
        || column.Split('_').Any(word => Array.IndexOf(SecretWords, word) >= 0);

    private static string KeyOf(EntityEntry entry, IKey key) =>
        string.Join('|', key.Properties.Select(p => Convert.ToString(entry.Property(p.Name).CurrentValue, System.Globalization.CultureInfo.InvariantCulture)));

    private List<NpgsqlCommand> BuildCommands(DbContext context, Pending pending)
    {
        var schema = context.Model.FindEntityType(typeof(Outbox.OutboxMessage))?.GetSchema()
            ?? throw new InvalidOperationException("An audited DbContext must map the module outbox table (HasOutboxTable).");
        var connection = (NpgsqlConnection)context.Database.GetDbConnection();
        var transaction = (NpgsqlTransaction?)context.Database.CurrentTransaction?.GetDbTransaction();

        var records = pending.Notes
            .Select(n => new AuditRecord(n.Table, n.Key ?? KeyOf(n.Entry, n.KeyDefinition), n.Operation, n.Columns))
            .ToList();

        var commands = new List<NpgsqlCommand>();
        foreach (var chunk in records.Chunk(AuditRecorded.MaxRecords))
        {
            var message = new AuditRecorded(Guid.NewGuid(), pending.OccurredAt, schema, pending.Actor, pending.CorrelationId, chunk);
            var command = new NpgsqlCommand(
                $"""
                INSERT INTO "{schema}"."OUTB_MSG" ("EVNT_TYP", "PYLD", "INSR_DTE", "AGGR_TYP", "MSG_VER")
                VALUES (@type, @payload, @now, @aggregate, @version)
                """,
                connection,
                transaction);
            command.Parameters.AddWithValue("type", nameof(AuditRecorded));
            command.Parameters.Add(new NpgsqlParameter("payload", NpgsqlDbType.Jsonb) { Value = JsonSerializer.Serialize(message) });
            command.Parameters.AddWithValue("now", pending.OccurredAt);
            command.Parameters.AddWithValue("aggregate", message.AggregateType);
            command.Parameters.AddWithValue("version", message.SchemaVersion);
            commands.Add(command);
        }

        return commands;
    }

    private sealed record Note(string Table, string Operation, IReadOnlyList<string> Columns, EntityEntry Entry, IKey KeyDefinition, string? Key);

    private sealed class Pending(List<Note> notes, string actor, string? correlationId, DateTimeOffset occurredAt)
    {
        public List<Note> Notes { get; } = notes;

        public string Actor { get; } = actor;

        public string? CorrelationId { get; } = correlationId;

        public DateTimeOffset OccurredAt { get; } = occurredAt;

        public bool NeedsOwnTransaction { get; init; }

        public IDbContextTransaction? OwnTransaction { get; set; }
    }
}
