using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Romp.BuildingBlocks.Application;
using Romp.BuildingBlocks.Persistence.Auditing;
using Romp.Modules.Customers.Application;
using Romp.Modules.Customers.Application.Abstractions;
using Romp.Modules.Customers.Application.Accounts;
using Romp.Modules.Customers.Infrastructure.Accounts;
using Romp.Modules.Customers.Infrastructure.Persistence;

namespace Romp.Modules.Customers.Tests.Support;

/// <summary>A clock the test moves by hand, so throttle delays and session lifetimes are exact.</summary>
internal sealed class SettableClock(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now += by;
}

/// <summary>
/// The sign-in pipeline (validation, then the transaction behaviour) over a real PostgreSQL, with a fast password hasher, the fake
/// captcha and a hand-set clock. Every Send uses its own scope, as concurrent requests do in production.
/// </summary>
internal sealed class AccountsHarness : IDisposable
{
    private readonly ServiceProvider _services;

    public AccountsHarness(CustomersPostgres postgres)
    {
        Clock = new SettableClock(DateTimeOffset.UtcNow);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<TimeProvider>(Clock);
        services.AddSingleton<ICurrentActor>(new TestActor());
        services.AddSingleton<AuditSaveChangesInterceptor>();
        services.AddDbContext<CustDbContext>((sp, options) => options
            .UseNpgsql(postgres.ConnectionString, npgsql => npgsql.MigrationsHistoryTable("MIG_HIST", "CUST"))
            .AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>()));
        services.AddScoped<ICustomerDbContext>(sp => sp.GetRequiredService<CustDbContext>());
        services.AddSingleton<IPasswordHasher>(new Argon2PasswordHasher(Options.Create(new Argon2Options { MemoryKiB = 8, Iterations = 1, Parallelism = 1 })));
        services.AddSingleton<ICaptchaVerifier, FakeCaptchaVerifier>();
        services.AddCustomerAccounts();

        var application = typeof(CustomersOptions).Assembly;
        services.AddValidatorsFromAssembly(application);
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(application);
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
            cfg.AddOpenBehavior(typeof(CustTransactionBehavior<,>));
        });
        _services = services.BuildServiceProvider();
    }

    public SettableClock Clock { get; }

    public async Task<T> Send<T>(IRequest<T> request)
    {
        await using var scope = _services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }

    public async Task Send(IRequest request)
    {
        await using var scope = _services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }

    /// <summary>Reads stored rows straight from the database, bypassing every handler.</summary>
    public async Task<T> Read<T>(Func<CustDbContext, Task<T>> query)
    {
        await using var scope = _services.CreateAsyncScope();
        return await query(scope.ServiceProvider.GetRequiredService<CustDbContext>());
    }

    /// <summary>Writes rows straight to the database, bypassing every handler (for set-up a handler cannot do).</summary>
    public async Task Seed(Action<CustDbContext> change)
    {
        await using var scope = _services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CustDbContext>();
        change(db);
        await db.SaveChangesAsync();
    }

    public void Dispose() => _services.Dispose();

    private sealed class TestActor : ICurrentActor
    {
        public string UserName => "test";
    }
}
