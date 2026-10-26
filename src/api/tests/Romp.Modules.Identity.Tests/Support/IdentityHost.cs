using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Romp.BuildingBlocks.Application;
using Romp.BuildingBlocks.Persistence;
using Romp.BuildingBlocks.Persistence.Auditing;
using Romp.BuildingBlocks.Persistence.Inbox;
using Romp.BuildingBlocks.Persistence.Outbox;
using Romp.Modules.Identity.Application;
using Romp.Modules.Identity.Application.Abstractions;
using Romp.Modules.Identity.Application.Mfa;
using Romp.Modules.Identity.Application.Sessions;
using Romp.Modules.Identity.Application.StaffAccounts;
using Romp.Modules.Identity.Domain.Roles;
using Romp.Modules.Identity.Domain.Sessions;
using Romp.Modules.Identity.Domain.StaffAccounts;
using Romp.Modules.Identity.Infrastructure.Mfa;
using Romp.Modules.Identity.Infrastructure.Persistence;
using Romp.Modules.Identity.Infrastructure.StaffAccounts;
using Romp.Modules.Identity.Tests.Roles;

namespace Romp.Modules.Identity.Tests.Support;

[CollectionDefinition(Name)]
public sealed class IdentityPostgresFixtureDefinition : ICollectionFixture<IdentityPostgres>
{
    public const string Name = "Identity PostgreSQL";
}

/// <summary>Records which sessions a handler asked to end; the real revoker arrives with the sessions feature.</summary>
internal sealed class RecordingSessionRevoker : IUserSessionRevoker
{
    public List<(long UserId, long? ExceptSessionId)> Calls { get; } = [];

    public List<SessionRevokeReason> Reasons { get; } = [];

    public Task RevokeAsync(long userId, long? exceptSessionId, SessionRevokeReason reason, CancellationToken cancellationToken)
    {
        Calls.Add((userId, exceptSessionId));
        Reasons.Add(reason);
        return Task.CompletedTask;
    }
}

/// <summary>Counts the SQL reader commands a context runs, to prove a list is a constant number of queries (no N+1, CONVENTIONS section 4).</summary>
internal sealed class CommandCounter : DbCommandInterceptor
{
    private int _count;

    public int Count => _count;

    public void Reset() => _count = 0;

    public override ValueTask<InterceptionResult<System.Data.Common.DbDataReader>> ReaderExecutingAsync(
        System.Data.Common.DbCommand command,
        CommandEventData eventData,
        InterceptionResult<System.Data.Common.DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _count);
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }
}

/// <summary>Records the Owner accounts whose MFA enrolment was cleared; the real reset arrives with the mfa feature.</summary>
internal sealed class RecordingMfaReset : IMfaEnrolmentReset
{
    public List<long> Cleared { get; } = [];

    public Task ClearAsync(long userId, CancellationToken cancellationToken)
    {
        Cleared.Add(userId);
        return Task.CompletedTask;
    }
}

/// <summary>Says which accounts have MFA enrolled; the real answer arrives with the mfa feature. Nobody is enrolled unless a test says so.</summary>
internal sealed class RecordingMfaStatus : IMfaEnrolmentStatus
{
    public HashSet<long> Enrolled { get; } = [];

    public Task<bool> IsEnrolledAsync(long userId, CancellationToken cancellationToken) => Task.FromResult(Enrolled.Contains(userId));
}

internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;
}

/// <summary>
/// The MediatR + FluentValidation pipeline <c>IdentityModule</c> wires in production, against a real PostgreSQL. The session revoker is a
/// recording fake until the sessions feature lands.
/// </summary>
internal sealed class IdentityHost : IDisposable
{
    public static readonly DateTimeOffset Start = new(2026, 10, 26, 9, 0, 0, TimeSpan.Zero);

    private static long _nameSeed;

    private readonly ServiceProvider _root;

    /// <param name="configure">Runs after the standard registrations, so a test can replace a service (for example with a counting spy).</param>
    public IdentityHost(IdentityPostgres postgres, CommandCounter? counter = null, Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSharedPersistence();
        services.AddScoped<OutboxMessageContext>();
        services.AddScoped<InboxGuard>();

        Time = new FixedTimeProvider(Start);
        services.AddSingleton<TimeProvider>(Time);
        Sessions = new RecordingSessionRevoker();
        services.AddSingleton<IUserSessionRevoker>(Sessions);
        MfaReset = new RecordingMfaReset();
        services.AddSingleton<IMfaEnrolmentReset>(MfaReset);
        Mfa = new RecordingMfaStatus();
        services.AddSingleton<IMfaEnrolmentStatus>(Mfa);
        services.AddSingleton(SessionLifetimes.Default);
        services.AddSingleton<ISecretProtector>(new AesGcmSecretProtector(Microsoft.Extensions.Options.Options.Create(new MfaOptions())));
        services.AddSingleton<IRecoveryPepper>(new ConfiguredRecoveryPepper(Microsoft.Extensions.Options.Options.Create(new MfaOptions())));
        services.AddScoped<SessionIssuer>();

        services.AddSingleton<IOptions<Argon2Options>>(
            Microsoft.Extensions.Options.Options.Create(new Argon2Options { MemoryKiB = 8, Iterations = 1, Parallelism = 1 }));
        services.AddSingleton<IPasswordHasher, Argon2PasswordHasher>();
        services.AddSingleton<SignInDummyHash>();
        services.AddSingleton<ITemporaryPasswordGenerator, TemporaryPasswordGenerator>();
        services.AddSingleton<ICommonPasswords, EmbeddedCommonPasswords>();
        services.AddSingleton<PasswordPolicy>();

        services.AddDbContext<IdntDbContext>((sp, options) =>
        {
            options
                .UseNpgsql(postgres.ConnectionString, npgsql => npgsql.MigrationsHistoryTable("MIG_HIST", "IDNT"))
                .AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>(), sp.GetRequiredService<OutboxSaveChangesInterceptor>());
            if (counter is not null)
            {
                options.AddInterceptors(counter);
            }
        });
        services.AddScoped<IIdentityDbContext>(sp => sp.GetRequiredService<IdntDbContext>());
        services.AddSingleton(NullLoggerFactory.Instance);

        var applicationAssembly = typeof(AssemblyReference).Assembly;
        services.AddValidatorsFromAssembly(applicationAssembly);
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(applicationAssembly);
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
            cfg.AddOpenBehavior(typeof(IdentityTransactionBehavior<,>));
        });

        configure?.Invoke(services);

        _root = services.BuildServiceProvider();
    }

    public FixedTimeProvider Time { get; }

    public RecordingSessionRevoker Sessions { get; }

    public RecordingMfaReset MfaReset { get; }

    public RecordingMfaStatus Mfa { get; }

    /// <summary>A user name no other test uses, so tests own their accounts although the database is shared.</summary>
    public static string NewUserName() =>
        string.Create(System.Globalization.CultureInfo.InvariantCulture, $"tester.{Interlocked.Increment(ref _nameSeed)}.{Environment.TickCount64 % 100000}");

    public AsyncServiceScope Scope() => _root.CreateAsyncScope();

    public async Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request)
    {
        await using var scope = Scope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }

    public async Task SendAsync(IRequest request)
    {
        await using var scope = Scope();
        await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }

    public async Task<short> RoleIdAsync(string code)
    {
        await using var scope = Scope();
        var db = scope.ServiceProvider.GetRequiredService<IdntDbContext>();
        return await db.Roles.AsNoTracking().Where(r => r.Code == code).Select(r => r.Id).SingleAsync();
    }

    public async Task<StaffUser> LoadAsync(long id)
    {
        await using var scope = Scope();
        var db = scope.ServiceProvider.GetRequiredService<IdntDbContext>();
        return await db.Staff.AsNoTracking().Include(u => u.Roles).Include(u => u.PasswordHistory).SingleAsync(u => u.Id == id);
    }

    public void Dispose() => _root.Dispose();
}
