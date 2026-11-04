using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Romp.BuildingBlocks.Application;
using Romp.BuildingBlocks.Email;
using Romp.BuildingBlocks.Persistence.Auditing;
using Romp.BuildingBlocks.Persistence.Inbox;
using Romp.BuildingBlocks.Persistence.Outbox;
using Romp.Modules.Customers.Application;
using Romp.Modules.Customers.Application.Abstractions;
using Romp.Modules.Customers.Application.Newsletter;
using Romp.Modules.Customers.Contracts;
using Romp.Modules.Customers.Domain.Abstractions;
using Romp.Modules.Customers.Domain.Accounts;
using Romp.Modules.Customers.Domain.Newsletter;
using Romp.Modules.Customers.Infrastructure.Newsletter;
using Romp.Modules.Customers.Infrastructure.Persistence;

namespace Romp.Modules.Customers.Tests.Support;

/// <summary>
/// The Customers pipeline with the real outbox interceptor, the real welcome mailer over a recording (and breakable) email sender, and a
/// way to deliver an outbox message to its consumer as the dispatcher would. Over a real PostgreSQL. Every call uses its own scope.
/// </summary>
internal sealed class NewsletterHarness : IDisposable
{
    public const string Key = "a-test-signing-key-long-enough-for-hmac";

    private readonly ServiceProvider _services;

    public NewsletterHarness(CustomersPostgres postgres)
    {
        Emails = new BreakableEmailSender();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<ICurrentActor>(new TestActor());
        services.AddSingleton<AuditSaveChangesInterceptor>();
        services.AddSingleton<OutboxSaveChangesInterceptor>();
        services.AddDbContext<CustDbContext>((sp, options) => options
            .UseNpgsql(postgres.ConnectionString, npgsql => npgsql.MigrationsHistoryTable("MIG_HIST", "CUST"))
            .AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>(), sp.GetRequiredService<OutboxSaveChangesInterceptor>()));
        services.AddScoped<ICustomerDbContext>(sp => sp.GetRequiredService<CustDbContext>());
        services.AddScoped<TestCurrentCustomer>();
        services.AddScoped<ICurrentCustomer>(sp => sp.GetRequiredService<TestCurrentCustomer>());
        services.AddSingleton(Options.Create(new CustomersOptions { LinkSigningKey = Key, PublicBaseUrl = "https://shop.example.test" }));
        services.AddSingleton<IEmailSender>(Emails);
        services.AddScoped<NewsletterConsent>();
        services.AddScoped<INewsletterMailer, EmailNewsletterMailer>();
        services.AddScoped<INewsletterAudience, NewsletterAudience>();
        services.AddScoped<OutboxMessageContext>();
        services.AddScoped<InboxGuard>();
        services.AddScoped<CustomerEmailConsumer>();

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

    public BreakableEmailSender Emails { get; }

    public static string NewEmail() => $"nl.{Guid.NewGuid():N}@example.test";

    public async Task<long> NewCustomerAsync(string? email = null)
    {
        await using var scope = _services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CustDbContext>();
        var customer = Customer.Create(email ?? NewEmail(), "Har", "Ness");
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        return customer.Id;
    }

    public async Task<T> Send<T>(IRequest<T> request, long? customerId = null)
    {
        await using var scope = _services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TestCurrentCustomer>().CustomerId = customerId;
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }

    public async Task Send(IRequest request, long? customerId = null)
    {
        await using var scope = _services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TestCurrentCustomer>().CustomerId = customerId;
        await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }

    /// <summary>Runs <paramref name="action"/> with a scope's services, e.g. to call the audience contract.</summary>
    public async Task<T> Use<T>(Func<IServiceProvider, Task<T>> action)
    {
        await using var scope = _services.CreateAsyncScope();
        return await action(scope.ServiceProvider);
    }

    public async Task<List<Stored>> StoredAsync(string? email = null)
    {
        await using var scope = _services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CustDbContext>();
        var query = db.Newsletter.AsNoTracking().Include(s => s.History).AsQueryable();
        if (email is not null)
        {
            query = query.Where(s => s.Email == email);
        }

        var rows = await query.OrderBy(s => s.Id).ToListAsync();
        return rows.Select(s => new Stored(
            s.Id, s.Email, s.Status, s.WelcomeSentAt, s.History.OrderBy(h => h.Id).Select(h => (h.Action, h.Source)).ToList())).ToList();
    }

    /// <summary>The CUST outbox rows for the email event, oldest first.</summary>
    public async Task<List<OutboxMessage>> EmailEventsAsync()
    {
        await using var scope = _services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CustDbContext>();
        return await db.Set<OutboxMessage>().AsNoTracking().Where(m => m.EventType == nameof(CustomerEmailRequestedEvent)).OrderBy(m => m.Id).ToListAsync();
    }

    /// <summary>Hands one stored outbox message to its consumer, the way the dispatcher does (its own scope, the row id in the context).</summary>
    public async Task DeliverAsync(OutboxMessage message)
    {
        var domainEvent = System.Text.Json.JsonSerializer.Deserialize<CustomerEmailRequestedEvent>(message.Payload)!;
        await using var scope = _services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<OutboxMessageContext>();
        context.MessageId = message.Id;
        context.Schema = "CUST";
        await scope.ServiceProvider.GetRequiredService<CustomerEmailConsumer>().HandleAsync(domainEvent, CancellationToken.None);
    }

    public void Dispose() => _services.Dispose();

    internal sealed record Stored(long Id, string Email, NewsletterStatus Status, DateTimeOffset? WelcomeSentAt, List<(ConsentAction Action, ConsentSource Source)> History);

    internal sealed class TestCurrentCustomer : ICurrentCustomer
    {
        public long? CustomerId { get; set; }
    }

    private sealed class TestActor : ICurrentActor
    {
        public string UserName => "test";
    }

    /// <summary>Records what is sent; can be told to fail, like an SMTP outage.</summary>
    internal sealed class BreakableEmailSender : IEmailSender
    {
        private readonly FakeEmailSender _inner = new();

        public bool Down { get; set; }

        public IReadOnlyCollection<EmailMessage> Sent => _inner.Sent;

        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken) =>
            Down ? throw new InvalidOperationException("SMTP is down.") : _inner.SendAsync(message, cancellationToken);
    }
}
