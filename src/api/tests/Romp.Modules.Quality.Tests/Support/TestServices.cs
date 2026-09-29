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

    public TestHost(string databaseName, Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSharedPersistence();
        services.AddScoped<OutboxMessageContext>();
        services.AddScoped<InboxGuard>();

        Time = new FixedTimeProvider(new DateTimeOffset(2026, 9, 29, 10, 0, 0, TimeSpan.Zero));
        services.AddSingleton<TimeProvider>(Time);

        services.AddDbContext<QcDbContext>((sp, options) => options
            .UseInMemoryDatabase(databaseName)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .AddInterceptors(
                sp.GetRequiredService<AuditSaveChangesInterceptor>(),
                sp.GetRequiredService<OutboxSaveChangesInterceptor>()));
        services.AddScoped<IQualityDbContext>(sp => sp.GetRequiredService<QcDbContext>());
        services.AddSingleton(new QualityOptions());

        PoQueries = new FakePurchaseOrderQueries();
        services.AddSingleton<IPurchaseOrderQueries>(PoQueries);
        services.AddSingleton<IGrnNumberAllocator>(new FakeGrnNumberAllocator());

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

    public void Dispose() => _root.Dispose();
}
