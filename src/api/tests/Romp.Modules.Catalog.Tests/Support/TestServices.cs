using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Romp.BuildingBlocks.Application;
using Romp.Modules.Catalog.Application;
using Romp.Modules.Catalog.Application.Abstractions;
using Romp.Modules.Catalog.Infrastructure.Persistence;
using Romp.Modules.Vendor.Contracts;

namespace Romp.Modules.Catalog.Tests.Support;

/// <summary>A fake VNDR contract - empty by default (no usage anywhere) unless a test seeds <see cref="Usage"/> (SCRUM-93, AC-2/AC-3).</summary>
internal sealed class FakePurchaseOrderUsageQueries : IPurchaseOrderUsageQueries
{
    public List<PoSizeColourUsage> Usage { get; } = [];

    public Task<IReadOnlyCollection<PoSizeColourUsage>> GetActiveSizeColourUsageAsync(
        long styleId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyCollection<PoSizeColourUsage>>(Usage);
}

/// <summary>
/// Builds the same MediatR + FluentValidation pipeline CatalogModule wires in production, but
/// against EF's InMemory provider (appropriate for our own command/validation logic, not
/// PostgreSQL-specific behaviour - CLAUDE.md reserves Testcontainers for that; see
/// CtlgMigrationTests for the one test here that needs real PostgreSQL).
/// </summary>
internal static class TestServices
{
    public static IServiceProvider Build(string databaseName, FakePurchaseOrderUsageQueries? poUsageQueries = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);

        services.AddDbContext<CatalogDbContext>(options => options
            .UseInMemoryDatabase(databaseName)
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning)));
        services.AddScoped<Romp.Modules.Catalog.Application.ProductMedia.PendingFileDeletes>();
        services.AddSingleton<Romp.BuildingBlocks.Storage.IFileStorage, InMemoryFileStorage>();
        services.AddScoped<ICatalogDbContext>(sp => sp.GetRequiredService<CatalogDbContext>());
        services.AddScoped<IPurchaseOrderUsageQueries>(_ => poUsageQueries ?? new FakePurchaseOrderUsageQueries());

        var applicationAssembly = typeof(AssemblyReference).Assembly;
        services.AddValidatorsFromAssembly(applicationAssembly);
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(applicationAssembly);
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
            cfg.AddOpenBehavior(typeof(CatalogTransactionBehavior<,>));
        });

        return services.BuildServiceProvider();
    }
}

/// <summary>An in-memory file store: keys are generated like the real one, never from a file name.</summary>
internal sealed class InMemoryFileStorage : Romp.BuildingBlocks.Storage.IFileStorage
{
    public Dictionary<string, byte[]> Files { get; } = [];

    public bool FailOnSave { get; set; }

    public Task<string> SaveAsync(byte[] content, CancellationToken cancellationToken)
    {
        if (FailOnSave)
        {
            throw new IOException("The disk is full.");
        }

        var key = Guid.NewGuid().ToString("N");
        Files[key] = content;
        return Task.FromResult(key);
    }

    public Task<byte[]> RetrieveAsync(string storageKey, CancellationToken cancellationToken) => Task.FromResult(Files[storageKey]);

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken)
    {
        Files.Remove(storageKey);
        return Task.CompletedTask;
    }
}
