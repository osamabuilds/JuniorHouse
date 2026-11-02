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
using Romp.Modules.Customers.Domain.Accounts;
using Romp.Modules.Customers.Infrastructure.Accounts;
using Romp.Modules.Customers.Infrastructure.Persistence;

namespace Romp.Modules.Customers.Tests.Support;

/// <summary>A clock the test moves by hand, so throttle delays, link lifetimes and session lifetimes are exact.</summary>
internal sealed class SettableClock(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now += by;
}

/// <summary>Remembers every link that would have been emailed; can be told to fail, as an unreachable mail server does.</summary>
internal sealed class RecordingMailer : ICustomerMailer
{
    private readonly List<(string Kind, string To, string Token)> _sent = [];

    public bool Fail { get; set; }

    public IReadOnlyList<(string Kind, string To, string Token)> Sent
    {
        get
        {
            lock (_sent)
            {
                return [.. _sent];
            }
        }
    }

    public Task SendVerificationAsync(string toEmail, string firstName, SecretString token, CancellationToken cancellationToken) =>
        Record("verify", toEmail, token);

    public Task SendPasswordResetAsync(string toEmail, string firstName, SecretString token, CancellationToken cancellationToken) =>
        Record("reset", toEmail, token);

    private Task Record(string kind, string to, SecretString token)
    {
        if (Fail)
        {
            throw new InvalidOperationException("The mail server is unreachable.");
        }

        lock (_sent)
        {
            _sent.Add((kind, to, token.Reveal()));
        }

        return Task.CompletedTask;
    }
}

/// <summary>Remembers who the sign-in hooks were told about; can be told to fail.</summary>
internal sealed class RecordingSignInHook : ICustomerSignedInHook
{
    private readonly List<SignedInContext> _calls = [];

    public bool Fail { get; set; }

    public IReadOnlyList<SignedInContext> Calls
    {
        get
        {
            lock (_calls)
            {
                return [.. _calls];
            }
        }
    }

    public Task OnSignedInAsync(SignedInContext context, CancellationToken cancellationToken)
    {
        lock (_calls)
        {
            _calls.Add(context);
        }

        return Fail ? throw new InvalidOperationException("The merge failed.") : Task.CompletedTask;
    }
}

/// <summary>
/// The sign-in pipeline (validation, then the transaction behaviour) over a real PostgreSQL, with a fast password hasher, the fake
/// captcha, a recording mailer and a hand-set clock. Every Send uses its own scope, as concurrent requests do in production.
/// </summary>
internal sealed class AccountsHarness : IDisposable
{
    private readonly ServiceProvider _services;

    public AccountsHarness(CustomersPostgres postgres)
    {
        Clock = new SettableClock(DateTimeOffset.UtcNow);
        Mailer = new RecordingMailer();
        Hook = new RecordingSignInHook();
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
        services.AddSingleton<ICustomerMailer>(Mailer);
        services.AddSingleton<ICustomerSignedInHook>(Hook);
        services.AddScoped<TestCurrentCustomer>();
        services.AddScoped<ICurrentCustomer>(sp => sp.GetRequiredService<TestCurrentCustomer>());
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

    public RecordingMailer Mailer { get; }

    public RecordingSignInHook Hook { get; }

    public async Task<T> Send<T>(IRequest<T> request, long? signedInAs = null)
    {
        await using var scope = _services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TestCurrentCustomer>().CustomerId = signedInAs;
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }

    public async Task Send(IRequest request, long? signedInAs = null)
    {
        await using var scope = _services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TestCurrentCustomer>().CustomerId = signedInAs;
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

    internal sealed class TestCurrentCustomer : ICurrentCustomer
    {
        public long? CustomerId { get; set; }
    }

    private sealed class TestActor : ICurrentActor
    {
        public string UserName => "test";
    }
}
