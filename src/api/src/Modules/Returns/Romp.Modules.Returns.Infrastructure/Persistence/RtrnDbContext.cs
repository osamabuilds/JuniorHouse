using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Romp.BuildingBlocks.Persistence.Auditing;
using Romp.BuildingBlocks.Persistence.Inbox;
using Romp.BuildingBlocks.Persistence.Outbox;
using Romp.Modules.Returns.Application.Abstractions;
using Romp.Modules.Returns.Domain;
using Romp.Modules.Returns.Domain.ReverseQc;

namespace Romp.Modules.Returns.Infrastructure.Persistence;

/// <summary>
/// EF Core context for the <c>RTRN</c> schema (return cases and their lines), including the module's outbox and inbox. Implements
/// <see cref="IReturnsDbContext"/> (Application's own port) so handlers never depend on this project.
/// </summary>
public sealed class RtrnDbContext(DbContextOptions<RtrnDbContext> options) : DbContext(options), IReturnsDbContext
{
    public DbSet<ReturnCase> Cases => Set<ReturnCase>();

    public DbSet<ReturnCaseLine> CaseLines => Set<ReturnCaseLine>();

    async Task IReturnsDbContext.SaveChangesAsync(CancellationToken cancellationToken) => await SaveChangesAsync(cancellationToken);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("RTRN");
        modelBuilder.HasOutboxTable("RTRN");
        modelBuilder.HasInboxTable("RTRN");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(RtrnDbContext).Assembly);
        ReturnsLookupConfiguration.Apply(modelBuilder);

        base.OnModelCreating(modelBuilder);
    }
}

/// <summary>Design-time factory so `dotnet ef migrations` can build a <see cref="RtrnDbContext"/> without the API host's DI container.</summary>
public sealed class RtrnDbContextFactory : IDesignTimeDbContextFactory<RtrnDbContext>
{
    public RtrnDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
            ?? "Host=localhost;Port=5433;Database=romp;Username=romp;Password=change-me-locally";

        var optionsBuilder = new DbContextOptionsBuilder<RtrnDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("MIG_HIST", "RTRN"));

        return new RtrnDbContext(optionsBuilder.Options);
    }
}

/// <summary>
/// Returns' unit of work: runs the handler inside one database transaction, then saves, so the state change, its inbox row and its outbox events
/// commit together (ADR 0004, NFR-FT-08). Applies only to <see cref="IReturnsCommand"/> requests.
/// </summary>
public sealed class ReturnsTransactionBehavior<TRequest, TResponse>(RtrnDbContext dbContext) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IReturnsCommand
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (dbContext.Database.CurrentTransaction is not null)
        {
            var nested = await next(cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            return nested;
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var response = await next(cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return response;
        }
        catch
        {
            // A failed unit of work must not leave tracked changes behind for a later command in the same scope.
            dbContext.ChangeTracker.Clear();
            throw;
        }
    }
}

internal sealed class ReturnCaseConfiguration : IEntityTypeConfiguration<ReturnCase>
{
    public void Configure(EntityTypeBuilder<ReturnCase> builder)
    {
        builder.ToTable("RTRN_MAIN");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(c => c.OrderId).HasColumnName("ORDR_ID").IsRequired();
        builder.Property(c => c.OrderNumber).HasColumnName("ORDR_NO").HasMaxLength(30).IsRequired();
        builder.Property(c => c.KindId).HasColumnName("RTRN_KIND_ID").IsRequired();
        builder.Property(c => c.StatusId).HasColumnName("RTRN_STS_ID").IsRequired();
        builder.Property(c => c.SealIntact).HasColumnName("SEAL_INTC_IND");
        builder.Property(c => c.ReceivingBinCode).HasColumnName("BIN_CODE").HasMaxLength(ReturnCase.MaxBinCodeLength);
        builder.Property(c => c.ReceivedBy).HasColumnName("RCVD_BY").HasMaxLength(100);
        builder.Property(c => c.ReceivedAt).HasColumnName("RCVD_DTE");
        builder.Property(c => c.CompletedBy).HasColumnName("CMPL_BY").HasMaxLength(100);
        builder.Property(c => c.CompletedAt).HasColumnName("CMPL_DTE");
        builder.Ignore(c => c.DomainEvents);

        // One case per order and kind (RQC-1): the database decides a race between two copies of the same event.
        builder.HasIndex(c => new { c.OrderId, c.KindId }).IsUnique().HasDatabaseName("UQ_RTRN_MAIN_ORDR_ID_RTRN_KIND_ID");
        // The queue reads one status, newest first.
        builder.HasIndex(c => new { c.StatusId, c.InsrDte }).HasDatabaseName("IX_RTRN_MAIN_RTRN_STS_ID_INSR_DTE");
        builder.HasIndex(c => c.KindId).HasDatabaseName("IX_RTRN_MAIN_RTRN_KIND_ID");
        builder.HasAuditColumns();

        builder.HasOne<ReturnKindLookup>().WithMany().HasForeignKey(c => c.KindId).HasConstraintName("FK_RTRN_MAIN_RTRN_KIND_LKP").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ReturnStatusLookup>().WithMany().HasForeignKey(c => c.StatusId).HasConstraintName("FK_RTRN_MAIN_RTRN_STS_LKP").OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(c => c.Lines).WithOne().HasForeignKey(l => l.CaseId).HasConstraintName("FK_RTRN_LINE_RTRN_MAIN").OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(c => c.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class ReturnCaseLineConfiguration : IEntityTypeConfiguration<ReturnCaseLine>
{
    public void Configure(EntityTypeBuilder<ReturnCaseLine> builder)
    {
        builder.ToTable("RTRN_LINE", table =>
        {
            table.HasCheckConstraint("CK_RTRN_LINE_ORDR_QTY", "\"ORDR_QTY\" > 0");
            table.HasCheckConstraint("CK_RTRN_LINE_DCSN_QTY", "\"DCSN_QTY\" IS NULL OR (\"DCSN_QTY\" >= 1 AND \"DCSN_QTY\" <= \"ORDR_QTY\")");
        });
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(l => l.CaseId).HasColumnName("RTRN_ID").IsRequired();
        builder.Property(l => l.VariantId).HasColumnName("VRNT_ID").IsRequired();
        builder.Property(l => l.Sku).HasColumnName("SKU").HasMaxLength(40);
        builder.Property(l => l.Name).HasColumnName("NAME").HasMaxLength(200).IsRequired();
        builder.Property(l => l.Colour).HasColumnName("CLR").HasMaxLength(60).IsRequired();
        builder.Property(l => l.Size).HasColumnName("SIZE").HasMaxLength(30).IsRequired();
        builder.Property(l => l.OrderedQuantity).HasColumnName("ORDR_QTY").IsRequired();
        builder.Property(l => l.DecisionId).HasColumnName("RTRN_DCSN_ID");
        builder.Property(l => l.DecidedQuantity).HasColumnName("DCSN_QTY");
        builder.Property(l => l.Note).HasColumnName("NOTE").HasMaxLength(ReturnCaseLine.MaxNoteLength);
        builder.Property(l => l.DecidedBy).HasColumnName("DCSN_BY").HasMaxLength(100);
        builder.Property(l => l.DecidedAt).HasColumnName("DCSN_DTE");
        builder.Ignore(l => l.IsDecided);

        builder.HasIndex(l => l.CaseId).HasDatabaseName("IX_RTRN_LINE_RTRN_ID");
        builder.HasIndex(l => l.DecisionId).HasDatabaseName("IX_RTRN_LINE_RTRN_DCSN_ID");
        builder.HasAuditColumns();

        builder.HasOne<ReturnDecisionLookup>().WithMany().HasForeignKey(l => l.DecisionId).HasConstraintName("FK_RTRN_LINE_RTRN_DCSN_LKP").OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>The system-owned Returns lookups (docs/db/naming.md standard lookup shape), seeded by migration.</summary>
internal static class ReturnsLookupConfiguration
{
    public static void Apply(ModelBuilder modelBuilder)
    {
        Configure<ReturnKindLookup>(modelBuilder, "RTRN_KIND_LKP",
            (ReturnKinds.Rto, "RTO", "Returned to origin", "The parcel came back to us from the courier"));

        Configure<ReturnStatusLookup>(modelBuilder, "RTRN_STS_LKP",
            (ReturnStatuses.AwaitingReceipt, "AWAITING_RECEIPT", "Awaiting receipt", "Waiting for the parcel to be received at the warehouse"),
            (ReturnStatuses.InInspection, "IN_INSPECTION", "In inspection", "Received; lines are being decided"),
            (ReturnStatuses.Completed, "COMPLETED", "Completed", "Every line is decided; the case is frozen"));

        Configure<ReturnDecisionLookup>(modelBuilder, "RTRN_DCSN_LKP",
            (ReturnDecisions.Restock, "RESTOCK", "Restock", "Back to sellable stock"),
            (ReturnDecisions.Seconds, "SECONDS", "Restock as seconds", "Back to stock as graded (concession) units"),
            (ReturnDecisions.Reject, "REJECT", "Reject", "Not restocked; a person resolves it"));
    }

    private static void Configure<TLookup>(ModelBuilder modelBuilder, string tableName, params (short Id, string Code, string Name, string Description)[] rows)
        where TLookup : ReturnsLookup
    {
        var builder = modelBuilder.Entity<TLookup>();
        builder.ToTable(tableName);
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).HasColumnName("ID").ValueGeneratedNever();
        builder.Property(l => l.Code).HasColumnName("CODE").HasMaxLength(30).IsRequired();
        builder.HasIndex(l => l.Code).IsUnique();
        builder.Property(l => l.Name).HasColumnName("NAME").HasMaxLength(100).IsRequired();
        builder.Property(l => l.Description).HasColumnName("DSCR").HasMaxLength(500);
        builder.Property(l => l.SortSeq).HasColumnName("SORT_SEQ").IsRequired();
        builder.Property(l => l.IsActive).HasColumnName("ACT_IND").IsRequired();
        builder.HasData(rows.Select(r => (object)new { Id = r.Id, Code = r.Code, Name = r.Name, Description = (string?)r.Description, SortSeq = r.Id, IsActive = true }).ToArray());
    }
}
