using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Romp.BuildingBlocks.Application;
using Romp.BuildingBlocks.Persistence;
using Romp.BuildingBlocks.Persistence.Auditing;
using Romp.BuildingBlocks.Persistence.Inbox;
using Romp.BuildingBlocks.Persistence.Outbox;
using Romp.Modules.Quality.Application;
using Romp.Modules.Quality.Application.Abstractions;
using Romp.Modules.Quality.Infrastructure.Persistence;

namespace Romp.Modules.Quality.Tests.Support;

internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;
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
            .AddInterceptors(
                sp.GetRequiredService<AuditSaveChangesInterceptor>(),
                sp.GetRequiredService<OutboxSaveChangesInterceptor>()));
        services.AddScoped<IQualityDbContext>(sp => sp.GetRequiredService<QcDbContext>());
        services.AddSingleton(new QualityOptions());

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

    public void Dispose() => _root.Dispose();
}
