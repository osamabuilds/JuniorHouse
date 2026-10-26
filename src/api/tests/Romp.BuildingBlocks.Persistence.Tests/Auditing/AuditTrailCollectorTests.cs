using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Romp.BuildingBlocks.Auditing;
using Romp.BuildingBlocks.Persistence.Auditing;
using Romp.BuildingBlocks.Persistence.Outbox;
using Testcontainers.PostgreSql;

namespace Romp.BuildingBlocks.Persistence.Tests.Auditing;

/// <summary>
/// The audit collector against real PostgreSQL (identity/audit-trail AC-2, AC-3, AC-6, AC-10): one record per changed entity, column
/// names only, secrets and technical tables left out, written to the outbox in the same transaction as the change.
/// </summary>
public sealed class AuditTrailCollectorTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 10, 26, 9, 0, 0, TimeSpan.Zero);

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await using var context = CreateContext("editor");
        await context.Database.EnsureCreatedAsync();
    }

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    private AuditProbeContext CreateContext(string actor) =>
        new(new DbContextOptionsBuilder<AuditProbeContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .AddInterceptors(
                new AuditSaveChangesInterceptor(new FakeTimeProvider(Now), new FakeCurrentActor(actor)),
                new AuditTrailSaveChangesInterceptor(new FakeTimeProvider(Now), new FakeCurrentActor(actor)))
            .Options);

    private async Task<List<AuditRecorded>> RecordedAsync()
    {
        await using var context = CreateContext("reader");
        var rows = await context.OutboxMessages.Where(m => m.EventType == nameof(AuditRecorded)).OrderBy(m => m.Id).ToListAsync();
        return rows.Select(r => JsonSerializer.Deserialize<AuditRecorded>(r.Payload)!).ToList();
    }

    [Fact]
    [Trait("Spec", "FR-59")]
    [Trait("Spec", "AC-2")]
    public async Task RecordsOneEntryPerEntity_WithItsGeneratedKey()
    {
        await using (var context = CreateContext("alice"))
        {
            context.Widgets.AddRange(new Widget { Label = "first" }, new Widget { Label = "second" });
            await context.SaveChangesAsync();
        }

        var recorded = Assert.Single(await RecordedAsync());
        Assert.Equal("TEST", recorded.Module);
        Assert.Equal("alice", recorded.Actor);
        Assert.Equal(2, recorded.Records.Count);
        Assert.All(recorded.Records, r =>
        {
            Assert.Equal("WIDG_MAIN", r.EntityType);
            Assert.Equal(AuditRecorded.Insert, r.Operation);
            Assert.True(long.Parse(r.EntityKey, System.Globalization.CultureInfo.InvariantCulture) > 0); // the key the database generated, not a temporary one
        });
        Assert.Equal(2, recorded.Records.Select(r => r.EntityKey).Distinct().Count());
    }

    [Fact]
    [Trait("Spec", "SEC-25")]
    [Trait("Spec", "AC-6")]
    public async Task StoresColumnNamesNotValues()
    {
        long id;
        await using (var context = CreateContext("alice"))
        {
            var widget = new Widget { Label = "a-very-distinctive-business-value" };
            context.Widgets.Add(widget);
            await context.SaveChangesAsync();
            id = widget.Id;
        }

        await using (var context = CreateContext("bob"))
        {
            var widget = await context.Widgets.SingleAsync(w => w.Id == id);
            widget.Label = "another-distinctive-value-after-the-edit";
            await context.SaveChangesAsync();
        }

        var update = (await RecordedAsync()).Last();
        var record = Assert.Single(update.Records);
        Assert.Equal(AuditRecorded.Update, record.Operation);
        Assert.Equal(["LABL"], record.Columns);

        await using var reader = CreateContext("reader");
        var raw = string.Concat(await reader.OutboxMessages.Select(m => m.Payload).ToListAsync());
        Assert.DoesNotContain("distinctive", raw);
    }

    [Fact]
    [Trait("Spec", "AC-6")]
    public async Task SecretColumns_AreExcludedEvenByName()
    {
        await using (var context = CreateContext("alice"))
        {
            context.Widgets.Add(new Widget { Label = "x", PasswordHash = "h", SecretCipher = "c", UnmarkedTokenDigest = "t" });
            await context.SaveChangesAsync();
        }

        var columns = Assert.Single(Assert.Single(await RecordedAsync()).Records).Columns;
        Assert.Contains("LABL", columns);
        Assert.DoesNotContain("PWD_HASH", columns); // marked with the attribute
        Assert.DoesNotContain("SECR_ENC", columns); // named like a secret
        Assert.DoesNotContain("DGST_TOKN", columns); // named like a secret, no attribute
    }

    [Fact]
    [Trait("Spec", "AC-2")]
    public async Task TechnicalTables_AreExcluded()
    {
        await using (var context = CreateContext("alice"))
        {
            context.Technical.Add(new TechnicalRow { Note = "noise" });
            await context.SaveChangesAsync();
        }

        Assert.Empty(await RecordedAsync());
    }

    [Fact]
    [Trait("Spec", "AC-2")]
    public async Task Deletes_AreRecordedWithTheirKey()
    {
        long id;
        await using (var context = CreateContext("alice"))
        {
            var widget = new Widget { Label = "gone" };
            context.Widgets.Add(widget);
            await context.SaveChangesAsync();
            id = widget.Id;
        }

        await using (var context = CreateContext("alice"))
        {
            context.Widgets.Remove(await context.Widgets.SingleAsync(w => w.Id == id));
            await context.SaveChangesAsync();
        }

        var record = Assert.Single((await RecordedAsync()).Last().Records);
        Assert.Equal(AuditRecorded.Delete, record.Operation);
        Assert.Equal(id.ToString(System.Globalization.CultureInfo.InvariantCulture), record.EntityKey);
    }

    [Fact]
    [Trait("Spec", "AC-10")]
    public async Task SplitsAtFiveHundred()
    {
        await using (var context = CreateContext("alice"))
        {
            context.Widgets.AddRange(Enumerable.Range(0, 501).Select(i => new Widget { Label = $"w{i}" }));
            await context.SaveChangesAsync();
        }

        var events = await RecordedAsync();
        Assert.Equal([500, 1], events.Select(e => e.Records.Count));
    }

    [Fact]
    [Trait("Spec", "AC-3")]
    public async Task RolledBackChange_LeavesNoEntry()
    {
        await using (var context = CreateContext("alice"))
        {
            await using var transaction = await context.Database.BeginTransactionAsync();
            context.Widgets.Add(new Widget { Label = "never" });
            await context.SaveChangesAsync();
            await transaction.RollbackAsync();
        }

        Assert.Empty(await RecordedAsync());
        await using var reader = CreateContext("reader");
        Assert.Empty(await reader.Widgets.ToListAsync());
    }

    [Fact]
    [Trait("Spec", "AC-3")]
    public async Task CommittedChange_AlwaysHasItsOutboxRow_WithoutACallerTransaction()
    {
        await using (var context = CreateContext("alice"))
        {
            context.Widgets.Add(new Widget { Label = "kept" });
            await context.SaveChangesAsync();
        }

        Assert.Single(await RecordedAsync());
        await using var reader = CreateContext("reader");
        Assert.Single(await reader.Widgets.ToListAsync());
    }

    [Fact]
    [Trait("Spec", "AC-3")]
    public async Task FailedSave_LeavesNeitherTheChangeNorAnEntry()
    {
        await using (var context = CreateContext("alice"))
        {
            context.Widgets.Add(new Widget { Label = "duplicate" });
            await context.SaveChangesAsync();
        }

        await using (var context = CreateContext("alice"))
        {
            context.Widgets.AddRange(new Widget { Label = "fine" }, new Widget { Label = "duplicate" });
            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        }

        Assert.Single(await RecordedAsync()); // only the first save
        await using var reader = CreateContext("reader");
        Assert.Single(await reader.Widgets.ToListAsync());
    }

    [Fact]
    [Trait("Spec", "AC-3")]
    public async Task ConnectionStaysUsable_AfterAFailedSave()
    {
        await using var context = CreateContext("alice");
        context.Widgets.Add(new Widget { Label = "dup" });
        await context.SaveChangesAsync();

        context.Widgets.Add(new Widget { Label = "dup" });
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        context.ChangeTracker.Clear();

        context.Widgets.Add(new Widget { Label = "after" });
        await context.SaveChangesAsync();

        Assert.Equal(2, (await RecordedAsync()).Count);
    }

    [Fact]
    [Trait("Spec", "AC-2")]
    public async Task SyncSave_IsAuditedToo()
    {
        await using (var context = CreateContext("alice"))
        {
            context.Widgets.Add(new Widget { Label = "sync" });
            context.SaveChanges();
        }

        Assert.Single(await RecordedAsync());
    }

    [Fact]
    [Trait("Spec", "AC-10")]
    public async Task OneMessagePerSave_ForUpToFiveHundredEntities()
    {
        await using var context = CreateContext("alice");
        context.Widgets.AddRange(Enumerable.Range(0, 500).Select(i => new Widget { Label = $"w{i}" }));
        await context.SaveChangesAsync();

        await using var connection = new NpgsqlConnection(_postgres.GetConnectionString());
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""SELECT count(*) FROM "TEST"."OUTB_MSG" """, connection);
        Assert.Equal(1L, await command.ExecuteScalarAsync());
    }
}

internal sealed class Widget
{
    public long Id { get; set; }

    public string Label { get; set; } = string.Empty;

    [AuditExcluded]
    public string? PasswordHash { get; set; }

    public string? SecretCipher { get; set; }

    public string? UnmarkedTokenDigest { get; set; }

    public DateTimeOffset InsrDte { get; set; }
}

[NotAudited("test technical table")]
internal sealed class TechnicalRow
{
    public long Id { get; set; }

    public string Note { get; set; } = string.Empty;
}

internal sealed class AuditProbeContext(DbContextOptions<AuditProbeContext> options) : DbContext(options)
{
    public DbSet<Widget> Widgets => Set<Widget>();

    public DbSet<TechnicalRow> Technical => Set<TechnicalRow>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Widget>(b =>
        {
            b.ToTable("WIDG_MAIN", "TEST");
            b.HasKey(w => w.Id);
            b.Property(w => w.Id).HasColumnName("ID").UseIdentityAlwaysColumn();
            b.Property(w => w.Label).HasColumnName("LABL").HasMaxLength(100).IsRequired();
            b.HasIndex(w => w.Label).IsUnique();
            b.Property(w => w.PasswordHash).HasColumnName("PWD_HASH");
            b.Property(w => w.SecretCipher).HasColumnName("SECR_ENC");
            b.Property(w => w.UnmarkedTokenDigest).HasColumnName("DGST_TOKN");
            b.Property(w => w.InsrDte).HasColumnName("INSR_DTE");
            b.Property<uint>("xmin").IsRowVersion(); // the same concurrency token every audited table has
        });

        modelBuilder.Entity<TechnicalRow>(b =>
        {
            b.ToTable("TECH_ROW", "TEST");
            b.HasKey(t => t.Id);
            b.Property(t => t.Id).HasColumnName("ID").UseIdentityAlwaysColumn();
            b.Property(t => t.Note).HasColumnName("NOTE");
        });

        modelBuilder.HasOutboxTable("TEST");
    }
}

/// <summary>AC-10: what auditing adds to a save, measured on a realistic batch (a putaway or a publish saves a few dozen rows at once).</summary>
public sealed class AuditTrailOverheadTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await using var context = Create(audited: false, "setup");
        await context.Database.EnsureCreatedAsync();
    }

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    private AuditProbeContext Create(bool audited, string tag)
    {
        var builder = new DbContextOptionsBuilder<AuditProbeContext>().UseNpgsql(_postgres.GetConnectionString());
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var actor = new FakeCurrentActor(tag);
        return new AuditProbeContext(audited
            ? builder.AddInterceptors(new AuditSaveChangesInterceptor(time, actor), new AuditTrailSaveChangesInterceptor(time, actor)).Options
            : builder.AddInterceptors(new AuditSaveChangesInterceptor(time, actor)).Options);
    }

    private async Task<double> MeasureAsync(bool audited, string prefix, int saves, int rowsPerSave)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        for (var i = 0; i < saves; i++)
        {
            await using var context = Create(audited, prefix);
            context.Widgets.AddRange(Enumerable.Range(0, rowsPerSave).Select(r => new Widget { Label = $"{prefix}-{i}-{r}" }));
            await context.SaveChangesAsync();
        }

        return watch.Elapsed.TotalMilliseconds;
    }

    [Fact]
    [Trait("Spec", "AC-10")]
    public async Task AddedSaveTime_StaysWithinATripwire_OnABatchSave()
    {
        await MeasureAsync(false, "warm-a", 10, 30);
        await MeasureAsync(true, "warm-b", 10, 30);

        // Best of three rounds each, interleaved, to keep a noisy neighbour from deciding the result.
        double plain = double.MaxValue, audited = double.MaxValue;
        for (var round = 0; round < 3; round++)
        {
            plain = Math.Min(plain, await MeasureAsync(false, $"p{round}", 40, 30));
            audited = Math.Min(audited, await MeasureAsync(true, $"a{round}", 40, 30));
        }

        // The audit row is written in the same transaction (BEGIN, INSERT, COMMIT are extra round trips on a one-statement save), so on a
        // pure-insert micro-benchmark the relative cost is high while the absolute cost is a few milliseconds. The guard is a loose relative tripwire against a gross regression such as a query per entity (measured 4 to 9 ms per save on loopback; machine load moves both sides).
        var perSave = (audited - plain) / 40;
        var overhead = (audited - plain) / plain;
        Assert.True(audited < (plain * 3) + 500, $"Auditing added {perSave:F1} ms ({overhead:P0}) to a 30-row save ({plain:F0} ms plain, {audited:F0} ms audited for 40 saves).");
    }
}
