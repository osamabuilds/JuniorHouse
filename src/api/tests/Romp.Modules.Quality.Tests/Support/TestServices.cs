using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Romp.BuildingBlocks.Application;
using Romp.BuildingBlocks.Persistence;
using Romp.BuildingBlocks.Persistence.Auditing;
using Romp.BuildingBlocks.Persistence.Inbox;
using Romp.BuildingBlocks.Persistence.Outbox;
using Romp.Modules.Quality.Application;
using Romp.Modules.Quality.Application.Abstractions;
using Romp.Modules.Quality.Infrastructure.Persistence;
using Romp.Modules.Vendor.Contracts;

namespace Romp.Modules.Quality.Tests.Support;

internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;
}

/// <summary>The PO as VNDR's contract reports it: a test sets exactly what QC may see (ADR 0002).</summary>
internal sealed class FakePurchaseOrderQueries : IPurchaseOrderQueries
{
    public const long PoId = 7001;
    public const string PoNo = "PO-2026-07001";

    public PoInForceTerms? Terms { get; set; } = DefaultTerms();

    public static PoInForceTerms DefaultTerms(short statusId = 3) => new(
        PoId, PoNo, VendorId: 1, StyleId: 500, statusId, RevisionNumber: 0,
        UnitCost: 100m, new DateOnly(2026, 10, 15), new DateOnly(2026, 10, 25), OverTolerancePercent: 5m, UnderTolerancePercent: 5m,
        PaymentTermId: 1, AdvancePercent: 0m, FabricResponsibilityId: 1,
        [new PoTermsLineContract(1, 10, 100), new PoTermsLineContract(2, 10, 50), new PoTermsLineContract(3, 10, 20)]);

    public Task<PoInForceTerms?> GetInForceTermsAsync(long poId, CancellationToken cancellationToken) => Task.FromResult(Terms);

    public Task<IReadOnlyList<PoRevisionSummary>> ListRevisionsAsync(long poId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<PoRevisionSummary>>([]);

    public Task<IReadOnlyList<PoEffectiveFile>> GetEffectiveFilesAsync(long poId, short? revisionNumber, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<PoEffectiveFile>>([]);

    public Task<PoProductionContext?> GetProductionContextAsync(long poId, CancellationToken cancellationToken) =>
        Task.FromResult<PoProductionContext?>(null);

    public Task<PoInForceTerms?> GetTermsAtAsync(long poId, DateOnly asOfDate, CancellationToken cancellationToken) => Task.FromResult(Terms);
}

/// <summary>A counter that stands in for the PostgreSQL upsert (raw SQL is not available on the InMemory provider).</summary>
internal sealed class FakeGrnNumberAllocator : IGrnNumberAllocator
{
    private int _next;

    public Task<string> AllocateAsync(CancellationToken cancellationToken) =>
        Task.FromResult($"GRN-2026-{Interlocked.Increment(ref _next):D5}");
}

/// <summary>
/// The same MediatR + FluentValidation pipeline QualityModule wires in production, against EF's InMemory provider
/// (appropriate for our own command/validation logic; PostgreSQL-specific behaviour is covered by the Testcontainers
/// tests). "Today" is 2026-09-29 in Pakistan time.
/// </summary>
internal sealed class TestHost : IDisposable
{
    public static readonly DateOnly Today = new(2026, 9, 29);

    private readonly ServiceProvider _root;

    public TestHost(string databaseName, Action<IServiceCollection>? configure = null, string? postgres = null)
    {
        // Every host gets its own InMemory database: two tests with the same name must never share rows.
        var inMemorySuffix = Guid.NewGuid().ToString("N");
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSharedPersistence();
        services.AddScoped<OutboxMessageContext>();
        services.AddScoped<InboxGuard>();

        Time = new FixedTimeProvider(new DateTimeOffset(2026, 9, 29, 10, 0, 0, TimeSpan.Zero));
        services.AddSingleton<TimeProvider>(Time);

        services.AddDbContext<QcDbContext>((sp, options) =>
        {
            if (postgres is null)
            {
                options.UseInMemoryDatabase(databaseName + inMemorySuffix).ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning));
            }
            else
            {
                options.UseNpgsql(postgres, npgsql => npgsql.MigrationsHistoryTable("MIG_HIST", "QC"));
            }

            options.AddInterceptors(
                sp.GetRequiredService<AuditSaveChangesInterceptor>(),
                sp.GetRequiredService<OutboxSaveChangesInterceptor>());
        });
        services.AddScoped<IQualityDbContext>(sp => sp.GetRequiredService<QcDbContext>());
        services.AddSingleton(new QualityOptions());

        PoQueries = new FakePurchaseOrderQueries();
        services.AddSingleton<IPurchaseOrderQueries>(PoQueries);
        if (postgres is null)
        {
            services.AddSingleton<IGrnNumberAllocator>(new FakeGrnNumberAllocator());
        }
        else
        {
            services.AddScoped<IGrnNumberAllocator, Romp.Modules.Quality.Infrastructure.Receiving.GrnNumberAllocator>();
        }

        var applicationAssembly = typeof(AssemblyReference).Assembly;
        services.AddValidatorsFromAssembly(applicationAssembly);
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(applicationAssembly);
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
            cfg.AddOpenBehavior(typeof(QcTransactionBehavior<,>));
        });

        configure?.Invoke(services);
        _root = services.BuildServiceProvider();

        // The InMemory provider applies HasData (the lookups and the AQL tables the migration seeds in PostgreSQL) only through EnsureCreated.
        if (postgres is null)
        {
            using var scope = _root.CreateScope();
            scope.ServiceProvider.GetRequiredService<QcDbContext>().Database.EnsureCreated();
        }
    }

    public FixedTimeProvider Time { get; }

    public FakePurchaseOrderQueries PoQueries { get; }

    /// <summary>A fresh scope per call, like one HTTP request each, so a missing Include cannot hide behind a shared, still-tracked DbContext.</summary>
    public async Task<T> SendAsync<T>(IRequest<T> request)
    {
        await using var scope = _root.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }

    public async Task SendAsync(IRequest request)
    {
        await using var scope = _root.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }

    public async Task<T> WithDbAsync<T>(Func<QcDbContext, Task<T>> action)
    {
        await using var scope = _root.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<QcDbContext>());
    }

    public async Task<T> WithScopeAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        await using var scope = _root.CreateAsyncScope();
        return await action(scope.ServiceProvider);
    }

    /// <summary>Stores an inbound delivery (PO 7001, lines size 1 / colour 10 = 100 and size 2 / colour 10 = 50) and returns its id.</summary>
    public async Task<long> AddDeliveryAsync(string dnNo = "DN-1", DateOnly? dispatch = null, long runId = 5) =>
        await SendAsync(new Romp.Modules.Quality.Application.InboundDeliveries.RecordInboundDeliveryCommand(
            runId, FakePurchaseOrderQueries.PoId, FakePurchaseOrderQueries.PoNo, dnNo, dispatch ?? new DateOnly(2026, 9, 20), false,
            [new(1, 10, 100), new(2, 10, 50)]));

    /// <summary>Records a GRN for a fresh delivery (claimed 100 + 50) with the given counts and returns it with the id of the QC batch it opened (0 when nothing was counted).</summary>
    public async Task<(Romp.Modules.Quality.Application.Receiving.GoodsReceiptDto Grn, long BatchId)> RecordGrnAsync(
        string dnNo = "DN-1", int line1 = 100, int line2 = 50)
    {
        var deliveryId = await AddDeliveryAsync(dnNo);
        var grn = await SendAsync(new Romp.Modules.Quality.Application.Receiving.RecordGoodsReceiptCommand(
            deliveryId, Today, "Sara Khan", null,
            [new(1, 10, line1), new(2, 10, line2)]));
        var batchId = await WithDbAsync(db => db.Batches.Where(b => b.GrnId == grn.Id).Select(b => b.Id).SingleOrDefaultAsync());
        return (grn, batchId);
    }

    public void Dispose() => _root.Dispose();
}

/// <summary>Builders for inspection input, so a test states only what is unusual about it.</summary>
internal static class InspectionData
{
    /// <summary>All six tech-pack items passed, except the listed check types, which failed.</summary>
    public static IReadOnlyList<Romp.Modules.Quality.Application.Batches.CheckResultDto> Checklist(params short[] failed) =>
        Romp.Modules.Quality.Domain.CheckType.All
            .Select(t => new Romp.Modules.Quality.Application.Batches.CheckResultDto(
                t, failed.Contains(t) ? Romp.Modules.Quality.Domain.CheckResult.Fail : Romp.Modules.Quality.Domain.CheckResult.Pass))
            .ToList();

    public static Romp.Modules.Quality.Application.Batches.DefectDto Defect(
        short severity, int qty, short type = Romp.Modules.Quality.Domain.DefectType.VisibleDefect, string note = "loose thread at hem") =>
        new(type, severity, qty, null, null, note);

    public static Romp.Modules.Quality.Application.Inspections.RecordInspectionCommand Command(
        long batchId,
        IReadOnlyList<Romp.Modules.Quality.Application.Batches.CheckResultDto>? checks = null,
        IReadOnlyList<Romp.Modules.Quality.Application.Batches.DefectDto>? defects = null,
        int? sample = null,
        long? subBatchId = null,
        string person = "Ali Raza") => new(
        batchId, subBatchId, new DateOnly(2026, 9, 29), person, sample, null, null, checks ?? Checklist(), defects ?? []);
}
