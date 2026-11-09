using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Options;
using Romp.Modules.Shipping.Application.Abstractions;

namespace Romp.Modules.Shipping.Infrastructure.Persistence;

/// <summary>Design-time factory so `dotnet ef migrations` can build a <see cref="ShpgDbContext"/> without the API host's DI container.</summary>
public sealed class ShpgDbContextFactory : IDesignTimeDbContextFactory<ShpgDbContext>
{
    public ShpgDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
            ?? "Host=localhost;Port=5433;Database=romp;Username=romp;Password=change-me-locally";

        var optionsBuilder = new DbContextOptionsBuilder<ShpgDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("MIG_HIST", "SHPG"));

        return new ShpgDbContext(optionsBuilder.Options);
    }
}

/// <summary>
/// Shipping's unit of work: runs the handler inside one database transaction, then saves, so the state change, its inbox row and its outbox events
/// commit together (ADR 0004, NFR-FT-08). A command sent from inside another command's handler joins the outer transaction. Applies only to
/// <see cref="IShippingCommand"/> requests.
/// </summary>
public sealed class ShippingTransactionBehavior<TRequest, TResponse>(ShpgDbContext dbContext) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IShippingCommand
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (dbContext.Database.CurrentTransaction is not null)
        {
            var nested = await next(cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            return nested;
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var response = await next(cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return response;
        }
        catch
        {
            // A failed unit of work must not leave tracked changes behind for a later command in the same scope.
            dbContext.ChangeTracker.Clear();
            throw;
        }
    }
}

/// <summary>Start-up validation of <c>Shipping:*</c>: a number outside its range stops the host from starting.</summary>
public sealed class ShippingOptionsValidator : IValidateOptions<ShippingOptions>
{
    public ValidateOptionsResult Validate(string? name, ShippingOptions options)
    {
        var failures = new List<string>();
        Check(options.RetryMinutes is < 1 or > 1440, "RetryMinutes must be between 1 and 1440", failures);
        Check(options.MaxAttempts is < 1 or > 200, "MaxAttempts must be between 1 and 200", failures);
        Check(options.PollMinutes is < 1 or > 1440, "PollMinutes must be between 1 and 1440", failures);
        Check(options.ManifestHour is < 0 or > 23, "ManifestHour must be between 0 and 23", failures);
        Check(options.CourierTimeoutSeconds is < 1 or > 120, "CourierTimeoutSeconds must be between 1 and 120", failures);
        Check(options.ClaimMinutes is < 1 or > 60, "ClaimMinutes must be between 1 and 60", failures);
        Check(options.TrackingFailuresBeforeCaptcha is < 1 or > 100, "TrackingFailuresBeforeCaptcha must be between 1 and 100", failures);
        foreach (var (code, settings) in options.Couriers)
        {
            if (settings.Enabled && settings.PushesStatus && settings.WebhookSecret.Length < 16)
            {
                failures.Add($"Shipping:Couriers:{code}:WebhookSecret must be at least 16 characters for a courier that pushes status.");
            }
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static void Check(bool bad, string message, List<string> failures)
    {
        if (bad)
        {
            failures.Add("Shipping:" + message + ".");
        }
    }
}
