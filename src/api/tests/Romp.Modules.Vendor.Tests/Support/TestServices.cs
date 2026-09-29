using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Romp.BuildingBlocks.Application;
using Romp.BuildingBlocks.Persistence;
using Romp.BuildingBlocks.Persistence.Auditing;
using Romp.BuildingBlocks.Persistence.Outbox;
using Romp.Modules.Catalog.Contracts;
using Romp.Modules.Reference.Contracts;
using Romp.Modules.Vendor.Application;
using Romp.Modules.Vendor.Application.Abstractions;
using Romp.Modules.Vendor.Application.Files;
using Romp.Modules.Vendor.Application.PurchaseOrders;
using Romp.Modules.Vendor.Contracts;
using Romp.Modules.Vendor.Domain.Vendors;
using Romp.Modules.Vendor.Infrastructure.Persistence;
using Romp.Modules.Vendor.Infrastructure.PurchaseOrders;

namespace Romp.Modules.Vendor.Tests.Support;

/// <summary>A fixed active style (id 500), size run [1,2], colourways [10,20] - matches what CreatePurchaseOrderCommandHandlerTests' valid PO lines reference.</summary>
internal sealed class FakeStyleQueries : IStyleQueries
{
    public static readonly StyleSummary ActiveStyle = new(500, "STY-FAKE", "Fake Style", [1, 2], [10, 20]);

    public Task<StyleSummary?> FindActiveStyleAsync(long styleId, CancellationToken cancellationToken) =>
        Task.FromResult(styleId == ActiveStyle.Id ? ActiveStyle : null);
}

/// <summary>Payment term 900 defaults to a 40% advance - matches what "no override" tests expect.</summary>
internal sealed class FakePaymentTermQueries : IPaymentTermQueries
{
    public const short TermId = 900;
    public const decimal DefaultAdvancePercent = 40m;

    public Task<decimal?> GetDefaultAdvancePercentAsync(short paymentTermId, CancellationToken cancellationToken) =>
        Task.FromResult(paymentTermId == TermId ? (decimal?)DefaultAdvancePercent : null);
}

/// <summary>
/// Builds the same MediatR + FluentValidation pipeline VendorModule wires in production, but
/// against EF's InMemory provider and fakes for the cross-module contracts (appropriate for our
/// own command/validation logic, not PostgreSQL-specific behaviour - CLAUDE.md reserves
/// Testcontainers for that; see VndrMigrationTests/PoMigrationTests/PoNumberAllocatorTests for the
/// tests here that need real PostgreSQL).
/// </summary>
internal static class TestServices
{
    public static IServiceProvider Build(string databaseName)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSharedPersistence();

        services.AddDbContext<VendorDbContext>((sp, options) => options
            .UseInMemoryDatabase(databaseName)
            .AddInterceptors(
                sp.GetRequiredService<AuditSaveChangesInterceptor>(),
                sp.GetRequiredService<OutboxSaveChangesInterceptor>()));
        services.AddScoped<IVendorDbContext>(sp => sp.GetRequiredService<VendorDbContext>());
        services.AddScoped<IStyleQueries, FakeStyleQueries>();
        services.AddScoped<IPaymentTermQueries, FakePaymentTermQueries>();
        services.AddScoped<Romp.Modules.Vendor.Contracts.IPurchaseOrderQueries, PurchaseOrderQueries>();
        services.AddScoped<IPoNumberAllocator, StubPoNumberAllocator>();
        services.AddSingleton(new PoCommercialTermsOptions());
        services.AddSingleton(new PoFileStorageOptions { MaxFileSizeBytes = 1024, MaxFilesPerPo = 3 });
        services.AddSingleton<IFileStorage, InMemoryFileStorage>();

        var applicationAssembly = typeof(AssemblyReference).Assembly;
        services.AddValidatorsFromAssembly(applicationAssembly);
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(applicationAssembly);
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
            cfg.AddOpenBehavior(typeof(VendorTransactionBehavior<,>));
        });

        return services.BuildServiceProvider();
    }
}

/// <summary>A deterministic, non-atomic stand-in for PoNumberAllocator - concurrency safety itself is proved separately in PoNumberAllocatorTests against real PostgreSQL.</summary>
internal sealed class StubPoNumberAllocator : IPoNumberAllocator
{
    private static int _counter;

    public Task<string> AllocateAsync(CancellationToken cancellationToken) =>
        Task.FromResult($"PO-2026-{Interlocked.Increment(ref _counter):D5}");
}
