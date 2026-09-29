using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Romp.BuildingBlocks.Application;
using Romp.BuildingBlocks.Persistence;
using Romp.BuildingBlocks.Persistence.Auditing;
using Romp.BuildingBlocks.Persistence.Inbox;
using Romp.BuildingBlocks.Persistence.Outbox;
using Romp.BuildingBlocks.Storage;
using Romp.Modules.Production.Application;
using Romp.Modules.Production.Application.Abstractions;
using Romp.Modules.Production.Application.ProductionRuns;
using Romp.Modules.Production.Infrastructure.Persistence;
using Romp.Modules.Vendor.Contracts;

namespace Romp.Modules.Production.Tests.Support;

/// <summary>The PO as VNDR's contract reports it - a test sets exactly what PROD may see (ADR 0002).</summary>
internal sealed class FakePurchaseOrderQueries : IPurchaseOrderQueries
{
    public const long PoId = 7001;
    public const string PoNo = "PO-2026-07001";

    public static readonly DateOnly SentDate = new(2026, 9, 1);

    public PoProductionContext? Context { get; set; } = new(PoId, PoNo, StatusId: 3, SentDate, FabricResponsibilityId: 1);

    public PoInForceTerms? Terms { get; set; } = DefaultTerms();

    /// <summary>Terms by dispatch date, to prove PROD asks for the revision in force on the dispatch date (D12).</summary>
    public Dictionary<DateOnly, PoInForceTerms> TermsAt { get; } = [];

    public List<DateOnly> TermsAtRequests { get; } = [];

    public static PoInForceTerms DefaultTerms(short revision = 0, DateOnly? expected = null, DateOnly? latest = null, decimal? over = 5m, decimal? under = 5m) => new(
        PoId, PoNo, VendorId: 1, StyleId: 500, StatusId: 3, revision,
        UnitCost: 100m, expected ?? new DateOnly(2026, 10, 15), latest ?? new DateOnly(2026, 10, 25), over, under,
        PaymentTermId: 1, AdvancePercent: 0m, FabricResponsibilityId: 1,
        [new PoTermsLineContract(1, 10, 100), new PoTermsLineContract(2, 10, 50)]);

    public Task<PoInForceTerms?> GetInForceTermsAsync(long poId, CancellationToken cancellationToken) => Task.FromResult(Terms);

    public Task<PoProductionContext?> GetProductionContextAsync(long poId, CancellationToken cancellationToken) => Task.FromResult(Context);

    public Task<PoInForceTerms?> GetTermsAtAsync(long poId, DateOnly asOfDate, CancellationToken cancellationToken)
    {
        TermsAtRequests.Add(asOfDate);
        return Task.FromResult(TermsAt.TryGetValue(asOfDate, out var terms) ? terms : Terms);
    }

    public Task<IReadOnlyList<PoRevisionSummary>> ListRevisionsAsync(long poId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<PoRevisionSummary>>([]);

    public Task<IReadOnlyList<PoEffectiveFile>> GetEffectiveFilesAsync(long poId, short? revisionNumber, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<PoEffectiveFile>>([]);
}

internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;
}

internal sealed class InMemoryFileStorage : IFileStorage
{
    private readonly Dictionary<string, byte[]> _files = [];

    public int Count => _files.Count;

    public Task<string> SaveAsync(byte[] content, CancellationToken cancellationToken)
    {
        var key = Guid.NewGuid().ToString("N");
        _files[key] = content;
        return Task.FromResult(key);
    }

    public Task<byte[]> RetrieveAsync(string storageKey, CancellationToken cancellationToken) => Task.FromResult(_files[storageKey]);
}

/// <summary>Real file signatures (the storage layer detects type from bytes, never from a name).</summary>
internal static class TestFileBytes
{
    public static byte[] Png() => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x01, 0x02];

    public static byte[] Pdf() => "%PDF-1.4 test"u8.ToArray();

    /// <summary>A Windows executable header renamed to look like a photo.</summary>
    public static byte[] DisguisedExecutable() => [0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00];
}

/// <summary>
/// The same MediatR + FluentValidation pipeline ProductionModule wires in production, against EF's InMemory
/// provider and a fake PO contract (appropriate for our own command/validation logic; PostgreSQL-specific
/// behaviour is covered by the Testcontainers tests). "Today" is 2026-09-29 in Pakistan time.
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

        services.AddDbContext<ProductionDbContext>((sp, options) => options
            .UseInMemoryDatabase(databaseName)
            .AddInterceptors(
                sp.GetRequiredService<AuditSaveChangesInterceptor>(),
                sp.GetRequiredService<OutboxSaveChangesInterceptor>()));
        services.AddScoped<IProductionDbContext>(sp => sp.GetRequiredService<ProductionDbContext>());

        PoQueries = new FakePurchaseOrderQueries();
        services.AddSingleton<IPurchaseOrderQueries>(PoQueries);
        Storage = new InMemoryFileStorage();
        services.AddSingleton<IFileStorage>(Storage);
        services.AddSingleton(new ProductionOptions { MaxFileSizeBytes = 1024, MaxFilesPerRound = 2 });

        var applicationAssembly = typeof(AssemblyReference).Assembly;
        services.AddValidatorsFromAssembly(applicationAssembly);
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(applicationAssembly);
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
            cfg.AddOpenBehavior(typeof(ProductionTransactionBehavior<,>));
        });

        configure?.Invoke(services);
        _root = services.BuildServiceProvider();
    }

    public FixedTimeProvider Time { get; }

    public FakePurchaseOrderQueries PoQueries { get; }

    public InMemoryFileStorage Storage { get; }

    /// <summary>A fresh scope per call, like one HTTP request each - so a missing Include cannot hide behind a shared, still-tracked DbContext.</summary>
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

    public async Task<T> WithDbAsync<T>(Func<ProductionDbContext, Task<T>> action)
    {
        await using var scope = _root.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<ProductionDbContext>());
    }

    public async Task<T> WithScopeAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        await using var scope = _root.CreateAsyncScope();
        return await action(scope.ServiceProvider);
    }

    /// <summary>Opens the run for the fake PO (acknowledged, VendorSupplied fabric unless changed first) and returns its id.</summary>
    public async Task<long> OpenRunAsync()
    {
        var id = await SendAsync(new OpenProductionRunCommand(FakePurchaseOrderQueries.PoId));
        return id!.Value;
    }

    public void Dispose() => _root.Dispose();
}
